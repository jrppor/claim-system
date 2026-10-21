using Claim.Application.Claims;
using Claim.Domain.Claims;

namespace Claim.Api.Tests;

/// <summary>In-memory read side over the fake Claim and Policy stores.</summary>
public sealed class FakeClaimListQueries(FakePolicyRepository policies, FakeClaimRepository claims) : IClaimListQueries
{
    public async Task<ClaimPage> SearchAsync(ClaimListQuery query, CancellationToken ct)
    {
        var rows = new List<(Domain.Claims.Claim Claim, Policy Policy)>();
        foreach (var c in claims.All.OrderByDescending(c => c.Id).Where(c => !(query.ExcludeDrafts && c.Status == ClaimStatus.Draft))
                     .Where(c => query.Status is null || c.Status == query.Status))
            rows.Add((c, (await policies.FindByIdAsync(c.PolicyId, ct))!));

        if (query.Search is { } text)
            rows = rows.Where(r => new[]
            {
                r.Claim.ClaimNumber, r.Claim.Details.VehiclePlate, r.Policy.Customer.FullName,
                r.Policy.Customer.Email, r.Claim.Details.CustomerEmail,
            }.Any(f => f is not null && f.Contains(text, StringComparison.OrdinalIgnoreCase))).ToList();

        var items = rows.Select(r => new ClaimListItem(
            r.Claim.Id, r.Claim.ClaimNumber, r.Claim.Status.ToString(), r.Policy.PolicyNumber,
            r.Claim.Details.VehiclePlate, r.Policy.Customer.FullName, r.Claim.Details.CustomerEmail,
            r.Claim.CreatedAt, r.Claim.UpdatedAt)).ToList();
        var paged = items.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();
        return new ClaimPage(paged, items.Count, query.Page, query.PageSize);
    }

    public Task<IReadOnlyDictionary<ClaimStatus, int>> CountByStatusAsync(bool excludeDrafts, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<ClaimStatus, int>>(claims.All
            .Where(c => !(excludeDrafts && c.Status == ClaimStatus.Draft))
            .GroupBy(c => c.Status).ToDictionary(g => g.Key, g => g.Count()));
}
