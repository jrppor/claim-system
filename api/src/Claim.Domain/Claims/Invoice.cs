namespace Claim.Domain.Claims;

/// <summary>
/// The Invoice a garage sends once a Claim is Repaired: the actual repair cost, to be set against the Approved Amount.
/// One per Claim; it is only ever added, never changed.
/// </summary>
public sealed class Invoice
{
    public long Id { get; init; }
    public long ClaimId { get; init; }
    public decimal ActualAmount { get; }
    public string? Note { get; }
    public long SubmittedByUserId { get; init; }
    public DateTime SubmittedAt { get; init; }

    public Invoice(decimal actualAmount, string? note)
    {
        ActualAmount = actualAmount;
        Note = note;
    }

    /// <summary>The same Invoice with the identity the store assigned.</summary>
    public Invoice Stored(long id) =>
        new(ActualAmount, Note) { Id = id, ClaimId = ClaimId, SubmittedByUserId = SubmittedByUserId, SubmittedAt = SubmittedAt };
}
