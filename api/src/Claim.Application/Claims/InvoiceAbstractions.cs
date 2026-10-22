using Claim.Domain.Claims;

namespace Claim.Application.Claims;

public interface IInvoiceRepository
{
    /// <summary>The Invoice of a Claim, or null when none has been submitted.</summary>
    Task<Invoice?> FindByClaimAsync(long claimId, CancellationToken ct);
}

/// <summary>A garage's Invoice: the actual repair cost and an optional note.</summary>
public sealed record InvoiceInput(decimal? ActualAmount, string? Note);

/// <summary>An Invoice shown against the Approved Amount; Difference is ActualAmount minus ApprovedAmount (positive means the garage billed more).</summary>
public sealed record InvoiceDto(
    long Id,
    long ClaimId,
    decimal ActualAmount,
    decimal? ApprovedAmount,
    decimal? Difference,
    string? Note,
    long SubmittedByUserId,
    DateTime SubmittedAt);
