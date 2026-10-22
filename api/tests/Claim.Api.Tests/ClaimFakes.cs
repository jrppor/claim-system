using Claim.Application.Claims;
using Claim.Domain.Claims;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Claim.Api.Tests;

/// <summary>
/// In-memory Claims that behave like the SQL table: reads return copies, and every write compares the caller's RowVersion
/// with the stored one (conflict -> <see cref="ConcurrencyException"/>), then bumps it, as SQL rowversion does.
/// </summary>
public sealed class FakeClaimRepository : IClaimRepository
{
    private readonly Dictionary<long, Domain.Claims.Claim> _claims = [];
    private long _sequence;
    private long _nextId;
    private long _version;
    private readonly object _lock = new();

    public IReadOnlyCollection<Domain.Claims.Claim> All { get { lock (_lock) return _claims.Values.Select(Copy).ToList(); } }

    public Task<long> NextClaimSequenceAsync(CancellationToken ct)
    {
        lock (_lock) return Task.FromResult(++_sequence);
    }

    public Task<long> AddAsync(Domain.Claims.Claim claim, CancellationToken ct)
    {
        lock (_lock)
        {
            var stored = Domain.Claims.Claim.CreateDraft(claim.ClaimNumber, claim.PolicyId, claim.Details, claim.CreatedByUserId, claim.CreatedAt);
            var withId = CopyWithId(stored, ++_nextId);
            withId.RowVersion = NextVersion();
            _claims[withId.Id] = withId;
            return Task.FromResult(withId.Id);
        }
    }

    /// <summary>Puts a Claim straight into the store in any status (the lifecycle endpoints arrive in later tickets).</summary>
    public Domain.Claims.Claim Seed(Domain.Claims.ClaimStatus status, long policyId, long createdByUserId, string plate = "1กก 1234", decimal? approvedAmount = null)
    {
        lock (_lock)
        {
            var id = ++_nextId;
            var claim = new Domain.Claims.Claim(status, new Domain.Claims.ClaimDetails(plate, "Toyota", "Camry", "a@b.test", "ชนท้าย"), DateTime.UtcNow)
            {
                Id = id,
                ClaimNumber = Domain.Claims.Claim.FormatNumber(2026, 900 + id),
                PolicyId = policyId,
                CreatedByUserId = createdByUserId,
                CreatedAt = DateTime.UtcNow,
                RowVersion = NextVersion(),
            };
            if (approvedAmount is { } amount) claim.RestoreApproval(amount, 1);
            _claims[id] = claim;
            return Copy(claim);
        }
    }

    public Task<Domain.Claims.Claim?> FindByIdAsync(long id, CancellationToken ct)
    {
        lock (_lock) return Task.FromResult(_claims.TryGetValue(id, out var c) ? Copy(c) : null);
    }

    /// <summary>Like <c>UPDATE ... WHERE Id = @Id AND RowVersion = @RowVersion</c>: zero rows touched means a conflict.</summary>
    public Task UpdateAsync(Domain.Claims.Claim claim, CancellationToken ct)
    {
        lock (_lock)
        {
            if (!_claims.TryGetValue(claim.Id, out var stored) || !stored.RowVersion.SequenceEqual(claim.RowVersion))
                throw new ConcurrencyException($"Claim {claim.ClaimNumber} was changed by someone else after it was read.");
            claim.RowVersion = NextVersion();
            _claims[claim.Id] = Copy(claim);
        }
        return Task.CompletedTask;
    }

    private byte[] NextVersion() => BitConverter.GetBytes(++_version);

    private static Domain.Claims.Claim Copy(Domain.Claims.Claim c)
    {
        var copy = CopyWithId(c, c.Id);
        copy.RowVersion = (byte[])c.RowVersion.Clone();
        if (c.ApprovedAmount is { } amount && c.ApprovedEstimateId is { } estimateId) copy.RestoreApproval(amount, estimateId);
        return copy;
    }

    private static Domain.Claims.Claim CopyWithId(Domain.Claims.Claim c, long id) =>
        new(c.Status, c.Details, c.UpdatedAt)
        {
            Id = id, ClaimNumber = c.ClaimNumber, PolicyId = c.PolicyId,
            CreatedByUserId = c.CreatedByUserId, CreatedAt = c.CreatedAt,
        };
}

public static class ClaimFakeRegistration
{
    public static IServiceCollection ReplaceClaimRepositories(
        this IServiceCollection services, FakePolicyRepository policies, FakeClaimRepository claims)
    {
        services.RemoveAll<IPolicyRepository>();
        services.AddSingleton<IPolicyRepository>(policies);
        services.RemoveAll<IClaimRepository>();
        services.AddSingleton<IClaimRepository>(claims);
        services.RemoveAll<IClaimListQueries>();
        services.AddSingleton<IClaimListQueries>(new FakeClaimListQueries(policies, claims));
        return services;
    }
}

public sealed class FakePolicyRepository : IPolicyRepository
{
    private readonly List<Policy> _policies = [];

    public Policy Add(string policyNumber, string plate, decimal coverageLimit, string customerEmail = "somsak@customer.test")
    {
        var id = _policies.Count + 1;
        var policy = new Policy(id, policyNumber, new Customer(id, "สมศักดิ์ ใจดี", customerEmail),
            plate, "Toyota", "Camry", coverageLimit);
        _policies.Add(policy);
        return policy;
    }

    public Task<IReadOnlyList<Policy>> SearchAsync(string text, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Policy>>(_policies.Where(p =>
            p.PolicyNumber.Contains(text, StringComparison.OrdinalIgnoreCase) ||
            p.PlateNumber.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList());

    public Task<Policy?> FindByIdAsync(long id, CancellationToken ct) =>
        Task.FromResult(_policies.FirstOrDefault(p => p.Id == id));
}
