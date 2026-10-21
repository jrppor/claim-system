using Claim.Application.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Claim.Api.Auth;

public sealed record LoginRequest(string? Email, string? Password);
public sealed record RefreshRequest(string? RefreshToken);

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/auth").WithTags("Auth");

        auth.MapPost("/login", async (LoginRequest req, AuthService service, CancellationToken ct) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (string.IsNullOrWhiteSpace(req.Email)) errors["email"] = ["Email is required."];
            if (string.IsNullOrEmpty(req.Password)) errors["password"] = ["Password is required."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);

            var result = await service.LoginAsync(req.Email!, req.Password!, ct);
            return result is null ? InvalidCredentials() : Results.Ok(result);
        }).AllowAnonymous();

        auth.MapPost("/refresh", async (RefreshRequest req, AuthService service, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.RefreshToken))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["refreshToken"] = ["Refresh token is required."] });
            var result = await service.RefreshAsync(req.RefreshToken, ct);
            return result is null
                ? Results.Problem(title: "Unauthorized", detail: "Refresh token is invalid or expired.", statusCode: StatusCodes.Status401Unauthorized)
                : Results.Ok(result);
        }).AllowAnonymous();

        // Anonymous on purpose: the access token may already have expired when the user signs out.
        // Possession of the refresh token is the credential; revoking an unknown token is a no-op.
        auth.MapPost("/logout", async (RefreshRequest req, AuthService service, CancellationToken ct) =>
        {
            if (!string.IsNullOrWhiteSpace(req.RefreshToken)) await service.LogoutAsync(req.RefreshToken, ct);
            return Results.NoContent();
        }).AllowAnonymous();

        auth.MapGet("/me", async (HttpContext http, AuthService service, CancellationToken ct) =>
        {
            var sub = http.User.FindFirst("sub")?.Value;
            var user = long.TryParse(sub, out var id) ? await service.GetUserAsync(id, ct) : null;
            return user is null ? Results.Unauthorized() : Results.Ok(user);
        }).RequireAuthorization();

        app.MapGet("/users", async (UserService users, CancellationToken ct) => Results.Ok(await users.ListAsync(ct)))
            .WithTags("Users")
            .RequireAuthorization(AuthPolicies.Admin);
    }

    private static IResult InvalidCredentials() =>
        Results.Problem(title: "Unauthorized", detail: "Invalid email or password.", statusCode: StatusCodes.Status401Unauthorized);
}
