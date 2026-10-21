using Claim.Domain.Claims;

namespace Claim.Application.Claims;

public interface IRepairEstimateRepository
{
    /// <summary>Every revision of the Claim's Repair Estimate, oldest first.</summary>
    Task<IReadOnlyList<RepairEstimate>> ListAsync(long claimId, CancellationToken ct);
}

/// <summary>An Insurance Adjuster's approval. Revision is optional; when given it must be the latest Repair Estimate revision.</summary>
public sealed record ApproveInput(decimal? ApprovedAmount, string? Reason, int? Revision);

public sealed record EstimateLineInput(string? Kind, string? Description, decimal? Quantity, decimal? UnitPrice);

/// <summary>Input of a Repair Estimate. There is deliberately no amount: the Estimated Amount is computed from the lines.</summary>
public sealed record EstimateInput(IReadOnlyList<EstimateLineInput>? Lines);

public sealed record EstimateLineDto(string Kind, string Description, decimal Quantity, decimal UnitPrice, decimal LineTotal);

public sealed record RepairEstimateDto(
    long Id,
    long ClaimId,
    int Revision,
    bool IsLatest,
    IReadOnlyList<EstimateLineDto> Lines,
    decimal EstimatedAmount,
    decimal CoverageLimit,
    bool ExceedsCoverageLimit,
    IReadOnlyList<string> Warnings,
    long SubmittedByUserId,
    DateTime SubmittedAt);
