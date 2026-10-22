using Claim.Api.Auth;
using Claim.Application.Claims;
using Claim.Domain.Claims;

namespace Claim.Api.Claims;

/// <summary>Endpoints that move a Claim through its lifecycle. One per transition so each can have its own role rule.</summary>
public static class ClaimStatusEndpoints
{
    public static void MapClaimStatusEndpoints(this IEndpointRouteBuilder app)
    {
        var claims = app.MapGroup("/claims").WithTags("Claims");

        claims.MapPost("/{id:long}/submit", async (long id, HttpContext http, ClaimWorkflowService workflow, CancellationToken ct) =>
        {
            try
            {
                var claim = await workflow.SubmitAsync(id, (await ReadOptionalBody(http, ct))?.Reason, long.Parse(http.User.FindFirst("sub")!.Value), http.User.FindFirst("role")!.Value, ct, http.Request.ExpectedVersion());
                return claim is null ? NotFound(id) : Results.Ok(claim);
            }
            catch (ClaimValidationException ex)
            {
                return Results.ValidationProblem(ex.Errors.ToDictionary(e => e.Key, e => e.Value));
            }
            catch (ClaimRuleException ex)
            {
                return InvalidTransition(ex);
            }
        }).RequireAuthorization(AuthPolicies.GarageStaff);

        claims.MapPost("/{id:long}/review", async (long id, HttpContext http, ClaimWorkflowService workflow, CancellationToken ct) =>
        {
            try
            {
                var claim = await workflow.StartReviewAsync(id, long.Parse(http.User.FindFirst("sub")!.Value), http.User.FindFirst("role")!.Value, ct, http.Request.ExpectedVersion());
                return claim is null ? NotFound(id) : Results.Ok(claim);
            }
            catch (ClaimRuleException ex)
            {
                return InvalidTransition(ex);
            }
        }).RequireAuthorization(AuthPolicies.InsuranceAdjuster);

        claims.MapPost("/{id:long}/approve", async (long id, ApproveInput input, HttpContext http, ClaimWorkflowService workflow, CancellationToken ct) =>
        {
            try
            {
                var claim = await workflow.ApproveAsync(id, input, long.Parse(http.User.FindFirst("sub")!.Value), http.User.FindFirst("role")!.Value, ct, http.Request.ExpectedVersion());
                return claim is null ? NotFound(id) : Results.Ok(claim);
            }
            catch (ClaimValidationException ex)
            {
                return Results.ValidationProblem(ex.Errors.ToDictionary(e => e.Key, e => e.Value));
            }
            catch (ClaimRuleException ex)
            {
                return InvalidTransition(ex);
            }
        }).RequireAuthorization(AuthPolicies.InsuranceAdjuster);

        claims.MapPost("/{id:long}/request-changes", async (long id, RequestChangesRequest body, HttpContext http, ClaimWorkflowService workflow, CancellationToken ct) =>
            await Decide(id, http, ct, (actor, role) => workflow.RequestChangesAsync(id, body.Comments, actor, role, ct, http.Request.ExpectedVersion())))
            .RequireAuthorization(AuthPolicies.InsuranceAdjuster);

        claims.MapPost("/{id:long}/reject", async (long id, StatusChangeRequest body, HttpContext http, ClaimWorkflowService workflow, CancellationToken ct) =>
            await Decide(id, http, ct, (actor, role) => workflow.RejectAsync(id, body.Reason, actor, role, ct, http.Request.ExpectedVersion())))
            .RequireAuthorization(AuthPolicies.InsuranceAdjuster);

        claims.MapPost("/{id:long}/cancel", async (long id, StatusChangeRequest body, HttpContext http, ClaimWorkflowService workflow, CancellationToken ct) =>
            await Decide(id, http, ct, (actor, role) => workflow.CancelAsync(id, body.Reason, actor, role, ct, http.Request.ExpectedVersion())))
            .RequireAuthorization(AuthPolicies.GarageStaff);

        claims.MapPost("/{id:long}/estimates", async (long id, EstimateInput input, HttpContext http, ClaimWorkflowService workflow, CancellationToken ct) =>
        {
            try
            {
                var estimate = await workflow.SubmitEstimateAsync(id, input, long.Parse(http.User.FindFirst("sub")!.Value), ct, http.Request.ExpectedVersion());
                return estimate is null ? NotFound(id) : Results.Created($"/claims/{id}/estimates", estimate);
            }
            catch (ClaimValidationException ex)
            {
                return Results.ValidationProblem(ex.Errors.ToDictionary(e => e.Key, e => e.Value));
            }
            catch (ClaimRuleException ex)
            {
                return InvalidTransition(ex);
            }
        }).RequireAuthorization(AuthPolicies.GarageStaff);

        claims.MapGet("/{id:long}/estimates", async (long id, HttpContext http, ClaimWorkflowService workflow, CancellationToken ct) =>
        {
            var revisions = await workflow.GetEstimatesAsync(id, http.User.FindFirst("role")!.Value, ct);
            return revisions is null ? NotFound(id) : Results.Ok(revisions);
        }).RequireAuthorization();

        claims.MapGet("/{id:long}/history", async (long id, HttpContext http, ClaimWorkflowService workflow, CancellationToken ct) =>
        {
            var history = await workflow.GetHistoryAsync(id, http.User.FindFirst("role")!.Value, ct);
            return history is null ? NotFound(id) : Results.Ok(history);
        }).RequireAuthorization();
    }

    public sealed record RequestChangesRequest(string? Comments);

    private static async Task<IResult> Decide(long id, HttpContext http, CancellationToken ct, Func<long, string, Task<ClaimDto?>> action)
    {
        try
        {
            var claim = await action(long.Parse(http.User.FindFirst("sub")!.Value), http.User.FindFirst("role")!.Value);
            return claim is null ? NotFound(id) : Results.Ok(claim);
        }
        catch (ClaimValidationException ex)
        {
            return Results.ValidationProblem(ex.Errors.ToDictionary(e => e.Key, e => e.Value));
        }
        catch (ClaimRuleException ex)
        {
            return InvalidTransition(ex);
        }
    }

    /// <summary>Optional body of a status-change call; the reason goes to Claim History and into the Customer email.</summary>
    public sealed record StatusChangeRequest(string? Reason);

    /// <summary>The body is optional, so clients that post nothing (any content type) are accepted rather than getting a 415.</summary>
    private static async Task<StatusChangeRequest?> ReadOptionalBody(HttpContext http, CancellationToken ct) =>
        http.Request.HasJsonContentType() && http.Request.ContentLength is > 0
            ? await http.Request.ReadFromJsonAsync<StatusChangeRequest>(ct)
            : null;

    private static IResult NotFound(long id) =>
        Results.Problem(title: "Not Found", detail: $"Claim {id} was not found.", statusCode: StatusCodes.Status404NotFound);

    private static IResult InvalidTransition(ClaimRuleException ex) =>
        Results.Problem(title: "Invalid status change", detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
}
