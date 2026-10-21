using System.Security.Cryptography;
using System.Text;
using Claim.Application.Auth;
using StackExchange.Redis;

namespace Claim.Infrastructure.Auth;

/// <summary>Refresh tokens in Redis, keyed by SHA-256 of the token so a Redis dump does not leak usable tokens.</summary>
public sealed class RedisRefreshTokenStore(Lazy<IConnectionMultiplexer> redis) : IRefreshTokenStore
{
    private IDatabase Db => redis.Value.GetDatabase();

    public Task SaveAsync(string token, long userId, TimeSpan lifetime, CancellationToken ct) =>
        Db.StringSetAsync(Key(token), userId, lifetime);

    public async Task<long?> ConsumeAsync(string token, CancellationToken ct)
    {
        // GETDEL is atomic, so a token can be consumed by at most one caller.
        var value = await Db.StringGetDeleteAsync(Key(token));
        return value.HasValue && long.TryParse(value.ToString(), out var id) ? id : null;
    }

    public Task RevokeAsync(string token, CancellationToken ct) => Db.KeyDeleteAsync(Key(token));

    private static RedisKey Key(string token) =>
        "refresh:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
