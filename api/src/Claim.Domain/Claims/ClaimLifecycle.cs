namespace Claim.Domain.Claims;

/// <summary>
/// The Claim lifecycle as a transition table. Every status change goes through <see cref="CanMove"/>
/// (via <see cref="Claim.ChangeStatus"/>), so later tickets add behaviour by using the table, not by re-deciding the rules.
/// Draft → Submitted → UnderReview ↔ ChangesRequested → Approved | Rejected;
/// Approved → InRepair → Repaired → InvoiceSubmitted → Paid.
/// Cancelled is allowed only before Approved. Rejected, Paid and Cancelled are final.
/// </summary>
public static class ClaimLifecycle
{
    private static readonly IReadOnlyDictionary<ClaimStatus, ClaimStatus[]> Allowed = new Dictionary<ClaimStatus, ClaimStatus[]>
    {
        [ClaimStatus.Draft] = [ClaimStatus.Submitted, ClaimStatus.Cancelled],
        [ClaimStatus.Submitted] = [ClaimStatus.UnderReview, ClaimStatus.Cancelled],
        [ClaimStatus.UnderReview] = [ClaimStatus.ChangesRequested, ClaimStatus.Approved, ClaimStatus.Rejected, ClaimStatus.Cancelled],
        [ClaimStatus.ChangesRequested] = [ClaimStatus.UnderReview, ClaimStatus.Cancelled],
        [ClaimStatus.Approved] = [ClaimStatus.InRepair],
        [ClaimStatus.Rejected] = [],
        [ClaimStatus.InRepair] = [ClaimStatus.Repaired],
        [ClaimStatus.Repaired] = [ClaimStatus.InvoiceSubmitted],
        [ClaimStatus.InvoiceSubmitted] = [ClaimStatus.Paid],
        [ClaimStatus.Paid] = [],
        [ClaimStatus.Cancelled] = [],
    };

    public static bool CanMove(ClaimStatus from, ClaimStatus to) => Allowed[from].Contains(to);

    public static IReadOnlyList<ClaimStatus> NextStatuses(ClaimStatus from) => Allowed[from];
}

/// <summary>One append-only line of Claim History: who moved the Claim between which statuses, when, and why.</summary>
public sealed record ClaimHistoryEntry(
    long ClaimId,
    ClaimStatus? FromStatus,
    ClaimStatus ToStatus,
    long ActorUserId,
    DateTime OccurredAt,
    string? Reason);
