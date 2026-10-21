using Claim.Api.Auth;
using Claim.Application.Claims;
using Claim.Domain.Claims;

namespace Claim.Api.Claims;

public static class ClaimEndpoints
{
    public static void MapClaimEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/policies", async (string? q, PolicyService policies, CancellationToken ct) =>
                Results.Ok(await policies.SearchAsync(q, ct)))
            .WithTags("Policies")
            .RequireAuthorization(AuthPolicies.GarageStaff);

        var claims = app.MapGroup("/claims").WithTags("Claims");

        claims.MapPost("/", async (ClaimInput input, HttpContext http, ClaimService service, CancellationToken ct) =>
        {
            try
            {
                var created = await service.CreateDraftAsync(input, UserId(http), ct);
                return Results.Created($"/claims/{created.Id}", created);
            }
            catch (ClaimValidationException ex)
            {
                return Results.ValidationProblem(ex.Errors.ToDictionary(e => e.Key, e => e.Value));
            }
        }).RequireAuthorization(AuthPolicies.GarageStaff);

        claims.MapGet("/{id:long}", async (long id, HttpContext http, ClaimService service, CancellationToken ct) =>
        {
            var claim = await service.GetAsync(id, http.User.FindFirst("role")!.Value, ct);
            return claim is null ? NotFound(id) : Results.Ok(claim);
        }).RequireAuthorization();

        claims.MapPut("/{id:long}", async (long id, ClaimInput input, ClaimService service, CancellationToken ct) =>
        {
            try
            {
                var claim = await service.EditDraftAsync(id, input, ct);
                return claim is null ? NotFound(id) : Results.Ok(claim);
            }
            catch (ClaimValidationException ex)
            {
                return Results.ValidationProblem(ex.Errors.ToDictionary(e => e.Key, e => e.Value));
            }
            catch (ClaimRuleException ex)
            {
                return Results.Problem(title: "Claim cannot be edited", detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
            }
        }).RequireAuthorization(AuthPolicies.GarageStaff);
    }

    private static IResult NotFound(long id) =>
        Results.Problem(title: "Not Found", detail: $"Claim {id} was not found.", statusCode: StatusCodes.Status404NotFound);

    private static long UserId(HttpContext http) => long.Parse(http.User.FindFirst("sub")!.Value);
}
