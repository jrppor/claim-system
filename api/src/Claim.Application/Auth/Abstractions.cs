using Claim.Domain.Users;

namespace Claim.Application.Auth;

public interface IUserRepository
{
    Task<User?> FindByEmailAsync(string email, CancellationToken ct);
    Task<User?> FindByIdAsync(long id, CancellationToken ct);
    Task<IReadOnlyList<User>> ListAsync(CancellationToken ct);
}

/// <summary>Refresh tokens live only in Redis (spec); implementations must make Consume atomic.</summary>
public interface IRefreshTokenStore
{
    Task SaveAsync(string token, long userId, TimeSpan lifetime, CancellationToken ct);

    /// <summary>Atomically reads and deletes the token. Returns the owning user id, or null if unknown/used/revoked.</summary>
    Task<long?> ConsumeAsync(string token, CancellationToken ct);

    Task RevokeAsync(string token, CancellationToken ct);
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
