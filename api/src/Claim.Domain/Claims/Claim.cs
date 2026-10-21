namespace Claim.Domain.Claims;

public enum ClaimStatus
{
    Draft,
    Submitted,
    UnderReview,
    ChangesRequested,
    Approved,
    Rejected,
    InRepair,
    Repaired,
    InvoiceSubmitted,
    Paid,
    Cancelled,
}

/// <summary>The editable details of a Draft Claim. Everything but the Policy may be blank until submit.</summary>
public sealed record ClaimDetails(
    string? VehiclePlate,
    string? VehicleMake,
    string? VehicleModel,
    string? CustomerEmail,
    string? DamageDescription);

/// <summary>Thrown when a rule of the Claim lifecycle forbids the requested change.</summary>
public sealed class ClaimRuleException(string message) : Exception(message);

public sealed class Claim
{
    public long Id { get; init; }
    public string ClaimNumber { get; init; } = "";
    public long PolicyId { get; init; }
    public ClaimStatus Status { get; private set; }
    public ClaimDetails Details { get; private set; }
    public long CreatedByUserId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; private set; }

    /// <summary>Optimistic-concurrency token (SQL rowversion). Enforced in a later ticket.</summary>
    public byte[] RowVersion { get; set; } = [];

    public Claim(ClaimStatus status, ClaimDetails details, DateTime updatedAt)
    {
        Status = status;
        Details = details;
        UpdatedAt = updatedAt;
    }

    public static Claim CreateDraft(string claimNumber, long policyId, ClaimDetails details, long createdByUserId, DateTime now) =>
        new(ClaimStatus.Draft, details, now)
        {
            ClaimNumber = claimNumber,
            PolicyId = policyId,
            CreatedByUserId = createdByUserId,
            CreatedAt = now,
        };

    /// <summary>Only a Draft may be edited; every later status is read-only here.</summary>
    public void EditDraft(ClaimDetails details, DateTime now)
    {
        if (Status != ClaimStatus.Draft)
            throw new ClaimRuleException($"Claim {ClaimNumber} is {Status} and can no longer be edited; only a Draft can.");
        Details = details;
        UpdatedAt = now;
    }

    /// <summary>Names of the details a Claim still lacks before it may leave Draft.</summary>
    public IReadOnlyList<string> MissingForSubmission()
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(Details.VehiclePlate)) missing.Add("vehiclePlate");
        if (string.IsNullOrWhiteSpace(Details.VehicleMake)) missing.Add("vehicleMake");
        if (string.IsNullOrWhiteSpace(Details.VehicleModel)) missing.Add("vehicleModel");
        if (string.IsNullOrWhiteSpace(Details.CustomerEmail)) missing.Add("customerEmail");
        if (string.IsNullOrWhiteSpace(Details.DamageDescription)) missing.Add("damageDescription");
        return missing;
    }

    /// <summary>
    /// The only way to change Status. Rejects moves the lifecycle table does not allow and returns the
    /// Claim History line that must be stored in the same transaction as the new status.
    /// </summary>
    public ClaimHistoryEntry ChangeStatus(ClaimStatus to, long actorUserId, string? reason, DateTime now)
    {
        if (!ClaimLifecycle.CanMove(Status, to))
            throw new ClaimRuleException($"Claim {ClaimNumber} is {Status} and cannot move to {to}.");
        var entry = new ClaimHistoryEntry(Id, Status, to, actorUserId, now, reason);
        Status = to;
        UpdatedAt = now;
        return entry;
    }

    /// <summary>An Insurance Adjuster never sees a Draft; Garage Staff and Admin see every Claim.</summary>
    public bool IsVisibleTo(string role) => Status != ClaimStatus.Draft || role != Users.Role.InsuranceAdjuster;

    public static string FormatNumber(int year, long sequenceValue) => $"CLM-{year}-{sequenceValue:D6}";
}
