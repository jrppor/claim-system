namespace Claim.Domain.Claims;

public enum EstimateLineKind
{
    Part,
    Labour,
}

/// <summary>One parts or labour line of a Repair Estimate. The line total is always derived, never supplied.</summary>
public sealed record EstimateLine(EstimateLineKind Kind, string Description, decimal Quantity, decimal UnitPrice)
{
    public decimal LineTotal => Math.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);
}

/// <summary>
/// One revision of the Repair Estimate for a Claim. Revisions are only ever added; the latest one is the revision under review.
/// The Estimated Amount is the sum of the line totals and cannot be set from outside.
/// </summary>
public sealed class RepairEstimate
{
    public long Id { get; init; }
    public long ClaimId { get; init; }
    public int Revision { get; init; }
    public IReadOnlyList<EstimateLine> Lines { get; }
    public long SubmittedByUserId { get; init; }
    public DateTime SubmittedAt { get; init; }

    public decimal EstimatedAmount => Lines.Sum(l => l.LineTotal);

    public RepairEstimate(IReadOnlyList<EstimateLine> lines) => Lines = lines;

    /// <summary>The same revision with the identity the store assigned.</summary>
    public RepairEstimate Stored(long id, int revision) =>
        new(Lines) { Id = id, ClaimId = ClaimId, Revision = revision, SubmittedByUserId = SubmittedByUserId, SubmittedAt = SubmittedAt };

    public bool Exceeds(decimal coverageLimit) => EstimatedAmount > coverageLimit;
}
