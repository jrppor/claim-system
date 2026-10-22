using Claim.Domain.Claims;
using Claim.Domain.Users;

namespace Claim.Application.Claims;

/// <summary>What the Claim list asks of the store. <c>ExcludeDrafts</c> is how role visibility reaches the query.</summary>
public sealed record ClaimListQuery(string? Search, ClaimStatus? Status, bool ExcludeDrafts, int Page, int PageSize);

public sealed record ClaimListItem(
    long Id,
    string ClaimNumber,
    string Status,
    string PolicyNumber,
    string? VehiclePlate,
    string CustomerName,
    string? CustomerEmail,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record ClaimPage(IReadOnlyList<ClaimListItem> Items, int Total, int Page, int PageSize);

public sealed record DashboardSummary(IReadOnlyDictionary<string, int> CountsByStatus, int Total);

/// <summary>Read side of the Claim list and dashboard (ADR 0003: plain service, no CQRS framework).</summary>
public interface IClaimListQueries
{
    Task<ClaimPage> SearchAsync(ClaimListQuery query, CancellationToken ct);

    /// <summary>Claim count per status, for statuses that have at least one Claim.</summary>
    Task<IReadOnlyDictionary<ClaimStatus, int>> CountByStatusAsync(bool excludeDrafts, CancellationToken ct);
}

public sealed class ClaimListService(IClaimListQueries queries)
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public Task<ClaimPage> ListAsync(string role, string? search, ClaimStatus? status, int? page, int? pageSize, CancellationToken ct)
    {
        var query = new ClaimListQuery(
            string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            status,
            ExcludeDrafts(role),
            Math.Max(page ?? 1, 1),
            Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize));
        return queries.SearchAsync(query, ct);
    }

    public async Task<DashboardSummary> SummaryAsync(string role, CancellationToken ct)
    {
        var counts = await queries.CountByStatusAsync(ExcludeDrafts(role), ct);
        var visible = Enum.GetValues<ClaimStatus>().Where(s => !(s == ClaimStatus.Draft && ExcludeDrafts(role)));
        var byStatus = visible.ToDictionary(s => s.ToString(), s => counts.GetValueOrDefault(s));
        return new DashboardSummary(byStatus, byStatus.Values.Sum());
    }

    // Dashboard and list share one rule per role: Garage Staff and Admin (read-only) count every status including Draft;
    // an Insurance Adjuster never sees a Draft, so Draft is left out of its counts and total (covered by ClaimListTests).
    private static bool ExcludeDrafts(string role) => role == Role.InsuranceAdjuster;
}
