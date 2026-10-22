using Claim.Api.Auth;
using Claim.Api.Http;
using Claim.Application.Claims;

namespace Claim.Api.Claims;

/// <summary>
/// Endpoints that move a Claim through its lifecycle, one per transition so each can have its own role rule.
/// Each demands the Claim version as If-Match (428 when absent). Validation and rule failures are thrown by the services and
/// become ProblemDetails in <see cref="ApiExceptionHandler"/>.
/// Messages here are English API messages (the UI words its own Thai text from the status code and field names).
/// </summary>
public static class ClaimStatusEndpoints
{
    public static void MapClaimStatusEndpoints(this IEndpointRouteBuilder app)
    {
        var claims = app.MapGroup("/claims").WithTags("Claims");

        claims.MapPost("/{id:long}/submit", async (long id, HttpContext http, ClaimWorkflowService workflow, CancellationToken ct) =>
        {
            var actor = Actor.From(http.User);
            var body = await ReadOptionalBody(http, ct);
            return Result(id, await workflow.SubmitAsync(id, body?.Reason, actor.UserId, actor.Role, http.Request.RequiredVersion(), ct));
        }).RequireAuthorization(AuthPolicies.GarageStaff);

        claims.MapPost("/{id:long}/review", async (long id, HttpContext http, ClaimWorkflowService workflow, CancellationToken ct) =>
        {
            var actor = Actor.From(http.User);
            return Result(id, await workflow.StartReviewAsync(id, actor.UserId, actor.Role, http.Request.RequiredVersion(), ct));
        }).RequireAuthorization(AuthPolicies.InsuranceAdjuster);

        claims.MapPost("/{id:long}/start-repair", async (long id, HttpContext http, ClaimWorkflowService workflow, CancellationToken ct) =>
        {
            var actor = Actor.From(http.User);
            return Result(id, await workflow.StartRepairAsync(id, actor.UserId, actor.Role, http.Request.RequiredVersion(), ct));
        }).RequireAuthorization(AuthPolicies.GarageStaff);

        claims.MapPost("/{id:long}/mark-repaired", async (long id, HttpContext http, ClaimWorkflowService workflow, CancellationToken ct) =>
        {
            var actor = Actor.From(http.User);
            return Result(id, await workflow.MarkRepairedAsync(id, actor.UserId, actor.Role, http.Request.RequiredVersion(), ct));
        }).RequireAuthorization(AuthPolicies.GarageStaff);

        claims.MapPost("/{id:long}/invoice", async (long id, InvoiceInput input, HttpContext http, ClaimWorkflowService workflow, CancellationToken ct) =>
        {
            var actor = Actor.From(http.User);
            var invoice = await workflow.SubmitInvoiceAsync(id, input, actor.UserId, actor.Role, http.Request.RequiredVersion(), ct);
            return invoice is null ? NotFound(id) : Results.Created($"/claims/{id}/invoice", invoice);
        }).RequireAuthorization(AuthPolicies.GarageStaff);

        claims.MapGet("/{id:long}/invoice", async (long id, HttpContext http, ClaimWorkflowService workflow, CancellationToken ct) =>
        {
            var invoice = await workflow.GetInvoiceAsync(id, Actor.From(http.User).Role, ct);
            return invoice is null
                ? Results.Problem(title: "Not Found", detail: $"Claim {id} has no Invoice.", statusCode: StatusCodes.Status404NotFound)
                : Results.Ok(invoice);
        }).RequireAuthorization();

        claims.MapPost("/{id:long}/pay", async (long id, HttpContext http, ClaimWorkflowService workflow, CancellationToken ct) =>
        {
            var actor = Actor.From(http.User);
            var body = await ReadOptionalBody(http, ct);
            return Result(id, await workflow.ConfirmPaymentAsync(id, body?.Reason, actor.UserId, actor.Role, http.Request.RequiredVersion(), ct));
        }).RequireAuthorization(AuthPolicies.InsuranceAdjuster);

        claims.MapPost("/{id:long}/approve", async (long id, ApproveInput input, HttpContext http, ClaimWorkflowService workflow, CancellationToken ct) =>
        {
            var actor = Actor.From(http.User);
            return Result(id, await workflow.ApproveAsync(id, input, actor.UserId, actor.Role, http.Request.RequiredVersion(), ct));
        }).RequireAuthorization(AuthPolicies.InsuranceAdjuster);

        claims.MapPost("/{id:long}/request-changes", async (long id, RequestChangesRequest body, HttpContext http, ClaimWorkflowService workflow, CancellationToken ct) =>
        {
            var actor = Actor.From(http.User);
            return Result(id, await workflow.RequestChangesAsync(id, body.Comments, actor.UserId, actor.Role, http.Request.RequiredVersion(), ct));
        }).RequireAuthorization(AuthPolicies.InsuranceAdjuster);

        claims.MapPost("/{id:long}/reject", async (long id, StatusChangeRequest body, HttpContext http, ClaimWorkflowService workflow, CancellationToken ct) =>
        {
            var actor = Actor.From(http.User);
            return Result(id, await workflow.RejectAsync(id, body.Reason, actor.UserId, actor.Role, http.Request.RequiredVersion(), ct));
        }).RequireAuthorization(AuthPolicies.InsuranceAdjuster);

        claims.MapPost("/{id:long}/cancel", async (long id, StatusChangeRequest body, HttpContext http, ClaimWorkflowService workflow, CancellationToken ct) =>
        {
            var actor = Actor.From(http.User);
            return Result(id, await workflow.CancelAsync(id, body.Reason, actor.UserId, actor.Role, http.Request.RequiredVersion(), ct));
        }).RequireAuthorization(AuthPolicies.GarageStaff);

        claims.MapPost("/{id:long}/estimates", async (long id, EstimateInput input, HttpContext http, ClaimWorkflowService workflow, CancellationToken ct) =>
        {
            var estimate = await workflow.SubmitEstimateAsync(id, input, Actor.From(http.User).UserId, http.Request.RequiredVersion(), ct);
            return estimate is null ? NotFound(id) : Results.Created($"/claims/{id}/estimates", estimate);
        }).RequireAuthorization(AuthPolicies.GarageStaff);

        claims.MapGet("/{id:long}/estimates", async (long id, HttpContext http, ClaimWorkflowService workflow, CancellationToken ct) =>
        {
            var revisions = await workflow.GetEstimatesAsync(id, Actor.From(http.User).Role, ct);
            return revisions is null ? NotFound(id) : Results.Ok(revisions);
        }).RequireAuthorization();

        claims.MapGet("/{id:long}/history", async (long id, HttpContext http, ClaimWorkflowService workflow, CancellationToken ct) =>
        {
            var history = await workflow.GetHistoryAsync(id, Actor.From(http.User).Role, ct);
            return history is null ? NotFound(id) : Results.Ok(history);
        }).RequireAuthorization();
    }

    public sealed record RequestChangesRequest(string? Comments);

    /// <summary>Optional body of a status-change call; the reason goes to Claim History and into the Customer email.</summary>
    public sealed record StatusChangeRequest(string? Reason);

    /// <summary>The body is optional, so clients that post nothing (any content type) are accepted rather than getting a 415.</summary>
    private static async Task<StatusChangeRequest?> ReadOptionalBody(HttpContext http, CancellationToken ct)
    {
        if (!http.Request.HasJsonContentType()) return null;
        // Content-Length is not reliable (a chunked request has none), so look at the body itself.
        http.Request.EnableBuffering();
        var isEmpty = await http.Request.Body.ReadAsync(new byte[1], ct) == 0;
        http.Request.Body.Position = 0;
        return isEmpty ? null : await http.Request.ReadFromJsonAsync<StatusChangeRequest>(ct);
    }

    private static IResult Result(long id, ClaimDto? claim) => claim is null ? NotFound(id) : Results.Ok(claim);

    private static IResult NotFound(long id) =>
        Results.Problem(title: "Not Found", detail: $"Claim {id} was not found.", statusCode: StatusCodes.Status404NotFound);
}
