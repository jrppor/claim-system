using Claim.Api.Auth;
using Claim.Api.Http;
using Claim.Application.Claims;

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
            var created = await service.CreateDraftAsync(input, Actor.From(http.User).UserId, ct);
            return Results.Created($"/claims/{created.Id}", created);
        }).RequireAuthorization(AuthPolicies.GarageStaff);

        claims.MapGet("/{id:long}", async (long id, HttpContext http, ClaimService service, CancellationToken ct) =>
        {
            var claim = await service.GetAsync(id, Actor.From(http.User).Role, ct);
            return claim is null ? NotFound(id) : Results.Ok(claim);
        }).RequireAuthorization();

        claims.MapPut("/{id:long}", async (long id, ClaimInput input, HttpContext http, ClaimService service, CancellationToken ct) =>
        {
            var claim = await service.EditDraftAsync(id, input, http.Request.RequiredVersion(), ct);
            return claim is null ? NotFound(id) : Results.Ok(claim);
        }).RequireAuthorization(AuthPolicies.GarageStaff);
    }

    private static IResult NotFound(long id) =>
        Results.Problem(title: "Not Found", detail: $"Claim {id} was not found.", statusCode: StatusCodes.Status404NotFound);
}
