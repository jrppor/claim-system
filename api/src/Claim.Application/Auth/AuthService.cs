using System.Security.Cryptography;
using Claim.Domain.Users;
using Microsoft.Extensions.Options;

namespace Claim.Application.Auth;

public sealed record UserInfo(long Id, string Email, string DisplayName, string Role);

public sealed record AuthResult(string AccessToken, int ExpiresIn, string RefreshToken, UserInfo User);

public sealed class AuthService(
    IUserRepository users,
    IRefreshTokenStore refreshTokens,
    IPasswordHasher hasher,
    IAccessTokenIssuer issuer,
    IOptions<AuthOptions> options)
{
    // Verified against when the email is unknown so both failures cost similar time.
    private readonly string _dummyHash = hasher.Hash("not-a-real-password");

    /// <summary>Returns null on any credential failure (unknown email, wrong password, inactive user).</summary>
    public async Task<AuthResult?> LoginAsync(string email, string password, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(email.Trim(), ct);
        var passwordOk = hasher.Verify(password, user?.PasswordHash ?? _dummyHash);
        if (user is null || !passwordOk || !user.IsActive) return null;
        return await IssueAsync(user, ct);
    }

    /// <summary>Rotates: the presented token is consumed and a new pair is issued. Null if token is invalid.</summary>
    public async Task<AuthResult?> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var userId = await refreshTokens.ConsumeAsync(refreshToken, ct);
        if (userId is null) return null;
        var user = await users.FindByIdAsync(userId.Value, ct);
        if (user is null || !user.IsActive) return null;
        return await IssueAsync(user, ct);
    }

    public Task LogoutAsync(string refreshToken, CancellationToken ct) =>
        refreshTokens.RevokeAsync(refreshToken, ct);

    public async Task<UserInfo?> GetUserAsync(long id, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(id, ct);
        return user is { IsActive: true } ? ToInfo(user) : null;
    }

    private async Task<AuthResult> IssueAsync(User user, CancellationToken ct)
    {
        var access = issuer.Issue(user);
        var refresh = Base64Url(RandomNumberGenerator.GetBytes(32));
        await refreshTokens.SaveAsync(refresh, user.Id, TimeSpan.FromDays(options.Value.RefreshTokenDays), ct);
        return new AuthResult(access.Value, access.ExpiresInSeconds, refresh, ToInfo(user));
    }

    internal static UserInfo ToInfo(User u) => new(u.Id, u.Email, u.DisplayName, u.Role);

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
