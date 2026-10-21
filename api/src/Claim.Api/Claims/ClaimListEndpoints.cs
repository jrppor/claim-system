using Claim.Application.Claims;
using Claim.Domain.Claims;

namespace Claim.Api.Claims;

/// <summary>Read-only endpoints: Claim list and dashboard summary. Open to every authenticated role; visibility is applied by role.</summary>
public static class ClaimListEndpoints
{
    public static void MapClaimListEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/claims", async (string? search, string? status, int? page, int? pageSize,
            HttpContext http, ClaimListService service, CancellationToken ct) =>
        {
            ClaimStatus? parsed = null;
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (!Enum.TryParse<ClaimStatus>(status, ignoreCase: true, out var s) || !Enum.IsDefined(s))
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = [$"Unknown status '{status}'."] });
                parsed = s;
            }
            return Results.Ok(await service.ListAsync(Role(http), search, parsed, page, pageSize, ct));
        }).WithTags("Claims").RequireAuthorization();

        app.MapGet("/dashboard/summary", async (HttpContext http, ClaimListService service, CancellationToken ct) =>
                Results.Ok(await service.SummaryAsync(Role(http), ct)))
            .WithTags("Dashboard").RequireAuthorization();
    }

    private static string Role(HttpContext http) => http.User.FindFirst("role")!.Value;
}
