using Claim.Api.Auth;
using Claim.Api.Http;
using Claim.Application.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Claim.Api.Claims;

public sealed record RequestChangesRequest(string? Comments);

/// <summary>Optional body of a status-change call; the reason goes to Claim History and into the Customer email.</summary>
public sealed record StatusChangeRequest(string? Reason);

/// <summary>
/// Actions that move a Claim through its lifecycle, one per transition so each can have its own role rule.
/// Each demands the Claim version as If-Match (428 when absent). Validation and rule failures are thrown by the services and
/// become ProblemDetails in <see cref="ApiExceptionHandler"/>.
/// Messages here are English API messages (the UI words its own Thai text from the status code and field names).
/// </summary>
[ApiController]
[Route("claims/{id:long}")]
[Tags("Claims")]
public sealed class ClaimWorkflowController(ClaimWorkflowService workflow) : ControllerBase
{
    [HttpPost("submit")]
    [Authorize(Policy = AuthPolicies.GarageStaff)]
    public async Task<IActionResult> Submit(long id, CancellationToken ct)
    {
        var actor = Actor.From(User);
        var body = await ReadOptionalBody(ct);
        return Result(id, await workflow.SubmitAsync(id, body?.Reason, actor.UserId, actor.Role, Request.RequiredVersion(), ct));
    }

    [HttpPost("review")]
    [Authorize(Policy = AuthPolicies.InsuranceAdjuster)]
    public async Task<IActionResult> StartReview(long id, CancellationToken ct)
    {
        var actor = Actor.From(User);
        return Result(id, await workflow.StartReviewAsync(id, actor.UserId, actor.Role, Request.RequiredVersion(), ct));
    }

    [HttpPost("start-repair")]
    [Authorize(Policy = AuthPolicies.GarageStaff)]
    public async Task<IActionResult> StartRepair(long id, CancellationToken ct)
    {
        var actor = Actor.From(User);
        return Result(id, await workflow.StartRepairAsync(id, actor.UserId, actor.Role, Request.RequiredVersion(), ct));
    }

    [HttpPost("mark-repaired")]
    [Authorize(Policy = AuthPolicies.GarageStaff)]
    public async Task<IActionResult> MarkRepaired(long id, CancellationToken ct)
    {
        var actor = Actor.From(User);
        return Result(id, await workflow.MarkRepairedAsync(id, actor.UserId, actor.Role, Request.RequiredVersion(), ct));
    }

    [HttpPost("invoice")]
    [Authorize(Policy = AuthPolicies.GarageStaff)]
    public async Task<IActionResult> SubmitInvoice(long id, InvoiceInput input, CancellationToken ct)
    {
        var actor = Actor.From(User);
        var invoice = await workflow.SubmitInvoiceAsync(id, input, actor.UserId, actor.Role, Request.RequiredVersion(), ct);
        return invoice is null ? this.ClaimNotFound(id) : Created($"/claims/{id}/invoice", invoice);
    }

    [HttpGet("invoice")]
    [Authorize]
    public async Task<IActionResult> GetInvoice(long id, CancellationToken ct)
    {
        var invoice = await workflow.GetInvoiceAsync(id, Actor.From(User).Role, ct);
        return invoice is null
            ? this.ProblemResult("Not Found", $"Claim {id} has no Invoice.", StatusCodes.Status404NotFound)
            : Ok(invoice);
    }

    [HttpPost("pay")]
    [Authorize(Policy = AuthPolicies.InsuranceAdjuster)]
    public async Task<IActionResult> ConfirmPayment(long id, CancellationToken ct)
    {
        var actor = Actor.From(User);
        var body = await ReadOptionalBody(ct);
        return Result(id, await workflow.ConfirmPaymentAsync(id, body?.Reason, actor.UserId, actor.Role, Request.RequiredVersion(), ct));
    }

    [HttpPost("approve")]
    [Authorize(Policy = AuthPolicies.InsuranceAdjuster)]
    public async Task<IActionResult> Approve(long id, ApproveInput input, CancellationToken ct)
    {
        var actor = Actor.From(User);
        return Result(id, await workflow.ApproveAsync(id, input, actor.UserId, actor.Role, Request.RequiredVersion(), ct));
    }

    [HttpPost("request-changes")]
    [Authorize(Policy = AuthPolicies.InsuranceAdjuster)]
    public async Task<IActionResult> RequestChanges(long id, RequestChangesRequest body, CancellationToken ct)
    {
        var actor = Actor.From(User);
        return Result(id, await workflow.RequestChangesAsync(id, body.Comments, actor.UserId, actor.Role, Request.RequiredVersion(), ct));
    }

    [HttpPost("reject")]
    [Authorize(Policy = AuthPolicies.InsuranceAdjuster)]
    public async Task<IActionResult> Reject(long id, StatusChangeRequest body, CancellationToken ct)
    {
        var actor = Actor.From(User);
        return Result(id, await workflow.RejectAsync(id, body.Reason, actor.UserId, actor.Role, Request.RequiredVersion(), ct));
    }

    [HttpPost("cancel")]
    [Authorize(Policy = AuthPolicies.GarageStaff)]
    public async Task<IActionResult> Cancel(long id, StatusChangeRequest body, CancellationToken ct)
    {
        var actor = Actor.From(User);
        return Result(id, await workflow.CancelAsync(id, body.Reason, actor.UserId, actor.Role, Request.RequiredVersion(), ct));
    }

    [HttpPost("estimates")]
    [Authorize(Policy = AuthPolicies.GarageStaff)]
    public async Task<IActionResult> SubmitEstimate(long id, EstimateInput input, CancellationToken ct)
    {
        var estimate = await workflow.SubmitEstimateAsync(id, input, Actor.From(User).UserId, Request.RequiredVersion(), ct);
        return estimate is null ? this.ClaimNotFound(id) : Created($"/claims/{id}/estimates", estimate);
    }

    [HttpGet("estimates")]
    [Authorize]
    public async Task<IActionResult> GetEstimates(long id, CancellationToken ct)
    {
        var revisions = await workflow.GetEstimatesAsync(id, Actor.From(User).Role, ct);
        return revisions is null ? this.ClaimNotFound(id) : Ok(revisions);
    }

    [HttpGet("history")]
    [Authorize]
    public async Task<IActionResult> GetHistory(long id, CancellationToken ct)
    {
        var history = await workflow.GetHistoryAsync(id, Actor.From(User).Role, ct);
        return history is null ? this.ClaimNotFound(id) : Ok(history);
    }

    /// <summary>The body is optional, so clients that post nothing (any content type) are accepted rather than getting a 415.</summary>
    private async Task<StatusChangeRequest?> ReadOptionalBody(CancellationToken ct)
    {
        if (!Request.HasJsonContentType()) return null;
        // Content-Length is not reliable (a chunked request has none), so look at the body itself.
        Request.EnableBuffering();
        var isEmpty = await Request.Body.ReadAsync(new byte[1], ct) == 0;
        Request.Body.Position = 0;
        return isEmpty ? null : await Request.ReadFromJsonAsync<StatusChangeRequest>(ct);
    }

    private IActionResult Result(long id, ClaimDto? claim) => claim is null ? this.ClaimNotFound(id) : Ok(claim);
}
