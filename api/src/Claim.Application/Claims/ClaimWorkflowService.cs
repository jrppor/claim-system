using Claim.Application.Auth;
using Claim.Domain.Claims;

namespace Claim.Application.Claims;

/// <summary>Status changes of a Claim. Each later lifecycle step (review, repair, payment, cancel) is another method here.</summary>
public sealed class ClaimWorkflowService(
    IClaimRepository claims,
    IPolicyRepository policies,
    IClaimHistoryRepository history,
    IUnitOfWork unitOfWork,
    IUserRepository users,
    IRepairEstimateRepository estimates,
    ClaimService claimService,
    TimeProvider clock)
{
    /// <summary>Draft → Submitted. Returns null when the Claim does not exist.</summary>
    public async Task<ClaimDto?> SubmitAsync(long id, string? reason, long actorUserId, string actorRole, CancellationToken ct)
    {
        var claim = await claims.FindByIdAsync(id, ct);
        if (claim is null) return null;

        reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        var errors = new Dictionary<string, string[]>();
        if (reason?.Length > 2000) errors["reason"] = ["Reason must be at most 2000 characters."];
        if (claim.Status == ClaimStatus.Draft)
            foreach (var field in claim.MissingForSubmission())
                errors[field] = [$"{field} is required to submit a Claim."];
        if (errors.Count > 0) throw new ClaimValidationException(errors);

        await ChangeStatusAsync(claim, ClaimStatus.Submitted, actorUserId, reason, ct);
        return await claimService.GetAsync(id, actorRole, ct);
    }

    /// <summary>Submitted → UnderReview: an Insurance Adjuster picks the Claim up. Returns null when the Claim does not exist or the role may not see it.</summary>
    public async Task<ClaimDto?> StartReviewAsync(long id, long actorUserId, string actorRole, CancellationToken ct)
    {
        var claim = await claims.FindByIdAsync(id, ct);
        if (claim is null || !claim.IsVisibleTo(actorRole)) return null;

        await ChangeStatusAsync(claim, ClaimStatus.UnderReview, actorUserId, null, ct);
        return await claimService.GetAsync(id, actorRole, ct);
    }

    /// <summary>UnderReview → Approved with an Approved Amount and reason. Returns null when the Claim does not exist or the role may not see it.</summary>
    public async Task<ClaimDto?> ApproveAsync(long id, ApproveInput input, long actorUserId, string actorRole, CancellationToken ct)
    {
        var claim = await claims.FindByIdAsync(id, ct);
        if (claim is null || !claim.IsVisibleTo(actorRole)) return null;

        var policy = (await policies.FindByIdAsync(claim.PolicyId, ct))!;
        var reason = CleanReason(input.Reason);
        var errors = new Dictionary<string, string[]>();
        if (reason is null) errors["reason"] = ["Reason is required to approve a Claim."];
        else if (reason.Length > 2000) errors["reason"] = ["Reason must be at most 2000 characters."];
        if (input.ApprovedAmount is null or <= 0 || HasMoreThanTwoDecimals(input.ApprovedAmount.Value))
            errors["approvedAmount"] = ["Approved Amount must be greater than 0 with at most 2 decimals."];
        else if (input.ApprovedAmount > policy.CoverageLimit)
            errors["approvedAmount"] = [$"Approved Amount {input.ApprovedAmount:0.00} exceeds the Policy Coverage Limit {policy.CoverageLimit:0.00}."];
        if (errors.Count > 0) throw new ClaimValidationException(errors);

        var revisions = await estimates.ListAsync(id, ct);
        var latest = revisions.MaxBy(e => e.Revision);
        if (claim.Status == ClaimStatus.UnderReview)
        {
            if (latest is null)
                throw new ClaimRuleException($"Claim {claim.ClaimNumber} has no Repair Estimate to approve.");
            if (input.Revision is { } revision && revision != latest.Revision)
                throw new ClaimRuleException($"Revision {revision} is not the latest Repair Estimate (revision {latest.Revision}) of Claim {claim.ClaimNumber}.");
        }

        var entry = claim.Approve(input.ApprovedAmount!.Value, policy.CoverageLimit, latest?.Id ?? 0, actorUserId, reason!, clock.GetUtcNow().UtcDateTime);
        await unitOfWork.RecordStatusChangeAsync(claim, entry, StatusEmailComposer.Compose(claim, policy, entry), ct);
        return await claimService.GetAsync(id, actorRole, ct);
    }

    /// <summary>UnderReview → ChangesRequested with the Adjuster's comments (kept as the History reason). Null when the Claim does not exist or the role may not see it.</summary>
    public async Task<ClaimDto?> RequestChangesAsync(long id, string? comments, long actorUserId, string actorRole, CancellationToken ct) =>
        await DecideAsync(id, ClaimStatus.ChangesRequested, "comments", comments, actorUserId, actorRole, ct);

    /// <summary>UnderReview → Rejected, a final status, with a reason. Null when the Claim does not exist or the role may not see it.</summary>
    public async Task<ClaimDto?> RejectAsync(long id, string? reason, long actorUserId, string actorRole, CancellationToken ct) =>
        await DecideAsync(id, ClaimStatus.Rejected, "reason", reason, actorUserId, actorRole, ct);

    private async Task<ClaimDto?> DecideAsync(long id, ClaimStatus to, string field, string? text, long actorUserId, string actorRole, CancellationToken ct)
    {
        var claim = await claims.FindByIdAsync(id, ct);
        if (claim is null || !claim.IsVisibleTo(actorRole)) return null;

        text = CleanReason(text);
        if (text is null) throw new ClaimValidationException(new Dictionary<string, string[]> { [field] = [$"A {field} is required."] });
        if (text.Length > 2000) throw new ClaimValidationException(new Dictionary<string, string[]> { [field] = [$"The {field} must be at most 2000 characters."] });

        await ChangeStatusAsync(claim, to, actorUserId, text, ct);
        return await claimService.GetAsync(id, actorRole, ct);
    }

    private static string? CleanReason(string? reason) => string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

    /// <summary>Creates the next Repair Estimate revision. Returns null when the Claim does not exist.</summary>
    public async Task<RepairEstimateDto?> SubmitEstimateAsync(long id, EstimateInput input, long actorUserId, CancellationToken ct)
    {
        var claim = await claims.FindByIdAsync(id, ct);
        if (claim is null) return null;
        var errors = ValidateEstimate(input);
        if (errors.Count > 0) throw new ClaimValidationException(errors);
        claim.EnsureAcceptsEstimate();
        var policy = (await policies.FindByIdAsync(claim.PolicyId, ct))!;

        var lines = input.Lines!.Select(l => new EstimateLine(Enum.Parse<EstimateLineKind>(l.Kind!), l.Description!.Trim(), l.Quantity!.Value, l.UnitPrice!.Value)).ToList();
        var now = clock.GetUtcNow().UtcDateTime;
        var draft = new RepairEstimate(lines) { ClaimId = id, SubmittedByUserId = actorUserId, SubmittedAt = now };

        // After ChangesRequested the garage has answered: the Claim goes back under review in the same transaction as the new
        // revision. From Submitted no Adjuster has picked it up yet, so only the revision is stored.
        ClaimHistoryEntry? entry = null;
        EmailMessage? email = null;
        if (claim.Status == ClaimStatus.ChangesRequested)
        {
            entry = claim.ChangeStatus(ClaimStatus.UnderReview, actorUserId, "ส่ง Repair Estimate ฉบับแก้ไขใหม่", now);
            email = StatusEmailComposer.Compose(claim, policy, entry);
        }
        var stored = await unitOfWork.RecordEstimateAsync(draft, entry is null ? null : claim, entry, email, ct);
        return ToDto(stored, policy, isLatest: true);
    }

    /// <summary>Every revision, oldest first, the latest flagged as the one under review. Null when the Claim does not exist or the role may not see it.</summary>
    public async Task<IReadOnlyList<RepairEstimateDto>?> GetEstimatesAsync(long id, string role, CancellationToken ct)
    {
        var claim = await claims.FindByIdAsync(id, ct);
        if (claim is null || !claim.IsVisibleTo(role)) return null;

        var policy = (await policies.FindByIdAsync(claim.PolicyId, ct))!;
        var revisions = await estimates.ListAsync(id, ct);
        var latest = revisions.Select(e => e.Revision).DefaultIfEmpty(0).Max();
        return revisions.Select(e => ToDto(e, policy, e.Revision == latest)).ToList();
    }

    private const int MaxEstimateLines = 100;

    private static Dictionary<string, string[]> ValidateEstimate(EstimateInput input)
    {
        var errors = new Dictionary<string, string[]>();
        if (input.Lines is null || input.Lines.Count == 0)
        {
            errors["lines"] = ["A Repair Estimate needs at least one parts or labour line."];
            return errors;
        }
        if (input.Lines.Count > MaxEstimateLines) errors["lines"] = [$"A Repair Estimate has at most {MaxEstimateLines} lines."];
        for (var i = 0; i < input.Lines.Count; i++)
        {
            var line = input.Lines[i];
            var at = $"lines[{i}]";
            if (line is null) { errors[at] = ["Line is required."]; continue; }
            if (!Enum.TryParse<EstimateLineKind>(line.Kind, out var kind) || !Enum.IsDefined(kind) || int.TryParse(line.Kind, out _))
                errors[$"{at}.kind"] = ["Kind must be Part or Labour."];
            if (string.IsNullOrWhiteSpace(line.Description)) errors[$"{at}.description"] = ["Description is required."];
            else if (line.Description.Trim().Length > 200) errors[$"{at}.description"] = ["Description must be at most 200 characters."];
            if (line.Quantity is null or <= 0 or > 100_000 || HasMoreThanTwoDecimals(line.Quantity.Value))
                errors[$"{at}.quantity"] = ["Quantity must be greater than 0, at most 100000, with at most 2 decimals."];
            if (line.UnitPrice is null or < 0 or > 10_000_000 || HasMoreThanTwoDecimals(line.UnitPrice.Value))
                errors[$"{at}.unitPrice"] = ["Unit price must be between 0 and 10000000 with at most 2 decimals."];
        }
        return errors;
    }

    private static bool HasMoreThanTwoDecimals(decimal value) => decimal.Round(value, 2) != value;

    private static RepairEstimateDto ToDto(RepairEstimate e, Policy policy, bool isLatest)
    {
        var exceeds = e.Exceeds(policy.CoverageLimit);
        return new RepairEstimateDto(
            e.Id, e.ClaimId, e.Revision, isLatest,
            e.Lines.Select(l => new EstimateLineDto(l.Kind.ToString(), l.Description, l.Quantity, l.UnitPrice, l.LineTotal)).ToList(),
            e.EstimatedAmount, policy.CoverageLimit, exceeds,
            exceeds ? [$"Estimated Amount {e.EstimatedAmount:0.00} exceeds the Policy Coverage Limit {policy.CoverageLimit:0.00}."] : [],
            e.SubmittedByUserId, e.SubmittedAt);
    }

    /// <summary>Returns null when the Claim does not exist or the role may not see it.</summary>
    public async Task<IReadOnlyList<ClaimHistoryDto>?> GetHistoryAsync(long id, string role, CancellationToken ct)
    {
        var claim = await claims.FindByIdAsync(id, ct);
        if (claim is null || !claim.IsVisibleTo(role)) return null;

        var entries = await history.ListAsync(id, ct);
        var names = new Dictionary<long, string>();
        foreach (var userId in entries.Select(e => e.ActorUserId).Distinct())
            names[userId] = (await users.FindByIdAsync(userId, ct))?.DisplayName ?? "";
        return entries
            .Select(e => new ClaimHistoryDto(e.FromStatus?.ToString(), e.ToStatus.ToString(), e.ActorUserId, names[e.ActorUserId], e.OccurredAt, e.Reason))
            .ToList();
    }

    /// <summary>The one path every transition takes: the domain checks the move, then status and history commit together.</summary>
    private async Task ChangeStatusAsync(Domain.Claims.Claim claim, ClaimStatus to, long actorUserId, string? reason, CancellationToken ct)
    {
        var entry = claim.ChangeStatus(to, actorUserId, reason, clock.GetUtcNow().UtcDateTime);
        var policy = (await policies.FindByIdAsync(claim.PolicyId, ct))!;
        await unitOfWork.RecordStatusChangeAsync(claim, entry, StatusEmailComposer.Compose(claim, policy, entry), ct);
    }
}
