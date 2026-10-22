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

    /// <summary>
    /// A Repair Estimate revision may be submitted while the Adjuster has not decided yet and is waiting on the garage:
    /// Submitted (first estimate) or ChangesRequested (revision). Not in Draft (the Claim is not submitted yet), and not once
    /// the Claim is decided or closed.
    /// UnderReview is allowed only when no estimate exists yet. Why: an Adjuster may pick a Claim up (Submitted → UnderReview)
    /// before the garage has sent any estimate; without this exception nobody could ever add one, because Approve needs an
    /// estimate and the garage could not submit while UnderReview. Once an estimate exists, the Adjuster is reading the latest
    /// revision, so a change must go through ChangesRequested.
    /// </summary>
    public static bool AcceptsEstimate(ClaimStatus status, bool hasEstimate) =>
        status is ClaimStatus.Submitted or ClaimStatus.ChangesRequested || (status == ClaimStatus.UnderReview && !hasEstimate);

    /// <summary>
    /// Claim Photos can be added or removed until the Claim is Approved: Draft, Submitted, UnderReview and ChangesRequested.
    /// From Approved on (and when Rejected or Cancelled) the evidence is locked, so it cannot change after the decision.
    /// </summary>
    public static bool AcceptsPhotoChange(ClaimStatus status) =>
        status is ClaimStatus.Draft or ClaimStatus.Submitted or ClaimStatus.UnderReview or ClaimStatus.ChangesRequested;

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
