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
