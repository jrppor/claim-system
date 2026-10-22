using Claim.Api.Http;
using Claim.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Claim.Api.Auth;

public sealed record LoginRequest(string? Email, string? Password);
public sealed record RefreshRequest(string? RefreshToken);

[ApiController]
[Route("auth")]
[Tags("Auth")]
public sealed class AuthController(AuthService service) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest req, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(req.Email)) errors["email"] = ["Email is required."];
        if (string.IsNullOrEmpty(req.Password)) errors["password"] = ["Password is required."];
        if (errors.Count > 0) return this.ValidationError(errors);

        var result = await service.LoginAsync(req.Email!, req.Password!, ct);
        return result is null
            ? this.ProblemResult("Unauthorized", "Invalid email or password.", StatusCodes.Status401Unauthorized)
            : Ok(result);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(RefreshRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.RefreshToken))
            return this.ValidationError(new Dictionary<string, string[]> { ["refreshToken"] = ["Refresh token is required."] });
        var result = await service.RefreshAsync(req.RefreshToken, ct);
        return result is null
            ? this.ProblemResult("Unauthorized", "Refresh token is invalid or expired.", StatusCodes.Status401Unauthorized)
            : Ok(result);
    }

    // Anonymous on purpose: the access token may already have expired when the user signs out.
    // Possession of the refresh token is the credential; revoking an unknown token is a no-op.
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(RefreshRequest req, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(req.RefreshToken)) await service.LogoutAsync(req.RefreshToken, ct);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var user = await service.GetUserAsync(Actor.From(User).UserId, ct);
        return user is null ? Unauthorized() : Ok(user);
    }
}
