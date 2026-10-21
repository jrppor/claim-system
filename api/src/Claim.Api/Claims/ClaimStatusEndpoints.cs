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
                var claim = await workflow.SubmitAsync(id, (await ReadOptionalBody(http, ct))?.Reason, long.Parse(http.User.FindFirst("sub")!.Value), http.User.FindFirst("role")!.Value, ct);
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

        claims.MapPost("/{id:long}/estimates", async (long id, EstimateInput input, HttpContext http, ClaimWorkflowService workflow, CancellationToken ct) =>
        {
            try
            {
                var estimate = await workflow.SubmitEstimateAsync(id, input, long.Parse(http.User.FindFirst("sub")!.Value), ct);
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
