using Claim.Application.Claims;
using Claim.Domain.Claims;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Claim.Api.Tests;

public sealed class FakeClaimRepository : IClaimRepository
{
    private readonly Dictionary<long, Domain.Claims.Claim> _claims = [];
    private long _sequence;
    private long _nextId;
    private readonly object _lock = new();

    public IReadOnlyCollection<Domain.Claims.Claim> All { get { lock (_lock) return _claims.Values.ToList(); } }

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
            _claims[withId.Id] = withId;
            return Task.FromResult(withId.Id);
        }
    }

    /// <summary>Puts a Claim straight into the store in any status (the lifecycle endpoints arrive in later tickets).</summary>
    public Domain.Claims.Claim Seed(Domain.Claims.ClaimStatus status, long policyId, long createdByUserId)
    {
        lock (_lock)
        {
            var id = ++_nextId;
            var claim = new Domain.Claims.Claim(status, new Domain.Claims.ClaimDetails("1กก 1234", "Toyota", "Camry", "a@b.test", "ชนท้าย"), DateTime.UtcNow)
            {
                Id = id,
                ClaimNumber = Domain.Claims.Claim.FormatNumber(2026, 900 + id),
                PolicyId = policyId,
                CreatedByUserId = createdByUserId,
                CreatedAt = DateTime.UtcNow,
            };
            _claims[id] = claim;
            return claim;
        }
    }

    public Task<Domain.Claims.Claim?> FindByIdAsync(long id, CancellationToken ct)
    {
        lock (_lock) return Task.FromResult(_claims.GetValueOrDefault(id));
    }

    public Task UpdateAsync(Domain.Claims.Claim claim, CancellationToken ct) => Task.CompletedTask;

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
