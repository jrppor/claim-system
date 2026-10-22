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

    /// <summary>What the Insurance Adjuster approved to pay. Stored apart from any Estimated Amount; null until the Claim is approved.</summary>
    public decimal? ApprovedAmount { get; private set; }

    /// <summary>The Repair Estimate revision that was the latest (the one under review) when the Claim was approved.</summary>
    public long? ApprovedEstimateId { get; private set; }

    /// <summary>
    /// Optimistic-concurrency token (SQL rowversion). Every write through the repositories / Unit of Work succeeds only if the
    /// stored version still equals this one, then refreshes it to the new stored version.
    /// </summary>
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

    /// <summary>Refuses a Repair Estimate revision unless the lifecycle accepts one in the current status.</summary>
    public void EnsureAcceptsEstimate(bool hasEstimate)
    {
        if (!ClaimLifecycle.AcceptsEstimate(Status, hasEstimate))
            throw new ClaimRuleException(Status == ClaimStatus.UnderReview
                ? $"Claim {ClaimNumber} is UnderReview and already has a Repair Estimate; a new revision can only be submitted after changes are requested."
                : $"Claim {ClaimNumber} is {Status}; a Repair Estimate can only be submitted while it is Submitted or ChangesRequested (or UnderReview with none yet).");
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

    /// <summary>
    /// UnderReview → Approved. The Approved Amount must be positive and within the Policy's Coverage Limit; it may be below the
    /// Estimated Amount. The approval is tied to the given (latest) Repair Estimate revision.
    /// </summary>
    public ClaimHistoryEntry Approve(decimal approvedAmount, decimal coverageLimit, long estimateId, long actorUserId, string reason, DateTime now)
    {
        if (approvedAmount <= 0 || approvedAmount > coverageLimit)
            throw new ClaimRuleException($"Approved Amount must be greater than 0 and at most the Coverage Limit {coverageLimit:0.00}.");
        var entry = ChangeStatus(ClaimStatus.Approved, actorUserId, reason, now);
        ApprovedAmount = approvedAmount;
        ApprovedEstimateId = estimateId;
        return entry;
    }

    /// <summary>
    /// Claim History line for a Claim Photo that was added. The status stays as it is (From and To are both the current status),
    /// but UpdatedAt moves so the Claim row is rewritten and its version refreshed.
    /// </summary>
    public ClaimHistoryEntry RecordPhotoAdded(string fileName, long actorUserId, DateTime now)
    {
        UpdatedAt = now;
        return new ClaimHistoryEntry(Id, Status, Status, actorUserId, now, $"อัปโหลดรูปความเสียหาย: {fileName}");
    }

    /// <summary>Reloads a stored approval when the Claim is read back from the database.</summary>
    public void RestoreApproval(decimal approvedAmount, long estimateId)
    {
        ApprovedAmount = approvedAmount;
        ApprovedEstimateId = estimateId;
    }

    /// <summary>An Insurance Adjuster never sees a Draft; Garage Staff and Admin see every Claim.</summary>
    public bool IsVisibleTo(string role) => Status != ClaimStatus.Draft || role != Users.Role.InsuranceAdjuster;

    public static string FormatNumber(int year, long sequenceValue) => $"CLM-{year}-{sequenceValue:D6}";
}
