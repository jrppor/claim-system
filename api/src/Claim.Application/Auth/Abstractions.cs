using Claim.Domain.Users;

namespace Claim.Application.Auth;

public interface IUserRepository
{
    Task<User?> FindByEmailAsync(string email, CancellationToken ct);
    Task<User?> FindByIdAsync(long id, CancellationToken ct);
    Task<IReadOnlyList<User>> ListAsync(CancellationToken ct);

    /// <summary>Inserts an active user. Returns null when the email is already taken.</summary>
    Task<User?> CreateAsync(string email, string passwordHash, string displayName, string role, CancellationToken ct);

    /// <summary>Returns false when no such user exists.</summary>
    Task<bool> SetActiveAsync(long id, bool isActive, CancellationToken ct);

    /// <summary>Returns false when no such user exists.</summary>
    Task<bool> SetPasswordHashAsync(long id, string passwordHash, CancellationToken ct);
}

/// <summary>Refresh tokens live only in Redis (spec); implementations must make Consume atomic.</summary>
public interface IRefreshTokenStore
{
    Task SaveAsync(string token, long userId, TimeSpan lifetime, CancellationToken ct);

    /// <summary>Atomically reads and deletes the token. Returns the owning user id, or null if unknown/used/revoked.</summary>
    Task<long?> ConsumeAsync(string token, CancellationToken ct);

    Task RevokeAsync(string token, CancellationToken ct);

    /// <summary>Revokes every outstanding refresh token of the user (disable, password reset).</summary>
    Task RevokeAllForUserAsync(long userId, CancellationToken ct);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public sealed record AccessToken(string Value, int ExpiresInSeconds);

public interface IAccessTokenIssuer
{
    AccessToken Issue(User user);
}
