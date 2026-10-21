using Claim.Domain.Users;

namespace Claim.Application.Auth;

public sealed record UserSummary(long Id, string Email, string DisplayName, string Role, bool IsActive);

public sealed class UserService(IUserRepository users, IPasswordHasher hasher, IRefreshTokenStore refreshTokens)
{
    public async Task<IReadOnlyList<UserSummary>> ListAsync(CancellationToken ct) =>
        (await users.ListAsync(ct)).Select(ToSummary).ToList();

    /// <summary>Returns null when the email is already in use.</summary>
    public async Task<UserSummary?> CreateAsync(string email, string displayName, string password, string role, CancellationToken ct)
    {
        var user = await users.CreateAsync(email.Trim(), hasher.Hash(password), displayName.Trim(), role, ct);
        return user is null ? null : ToSummary(user);
    }

    /// <summary>Deactivates the user and revokes their refresh tokens. Returns false when the user does not exist.</summary>
    public async Task<bool> DisableAsync(long id, CancellationToken ct)
    {
        if (!await users.SetActiveAsync(id, false, ct)) return false;
        await refreshTokens.RevokeAllForUserAsync(id, ct);
        return true;
    }

    public const int MinPasswordLength = 8;

    /// <summary>Sets a new password and revokes refresh tokens. Returns false when the user does not exist.</summary>
    public async Task<bool> ResetPasswordAsync(long id, string newPassword, CancellationToken ct)
    {
        if (!await users.SetPasswordHashAsync(id, hasher.Hash(newPassword), ct)) return false;
        await refreshTokens.RevokeAllForUserAsync(id, ct);
        return true;
    }

    private static UserSummary ToSummary(User u) => new(u.Id, u.Email, u.DisplayName, u.Role, u.IsActive);
}
