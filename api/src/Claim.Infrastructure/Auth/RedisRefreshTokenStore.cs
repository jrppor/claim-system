using System.Security.Cryptography;
using System.Text;
using Claim.Application.Auth;
using StackExchange.Redis;

namespace Claim.Infrastructure.Auth;

/// <summary>Refresh tokens in Redis, keyed by SHA-256 of the token so a Redis dump does not leak usable tokens.</summary>
public sealed class RedisRefreshTokenStore(Lazy<IConnectionMultiplexer> redis) : IRefreshTokenStore
{
    private IDatabase Db => redis.Value.GetDatabase();

    public async Task SaveAsync(string token, long userId, TimeSpan lifetime, CancellationToken ct)
    {
        var key = Key(token);
        await Db.StringSetAsync(key, userId, lifetime);
        // Per-user index so all of a user's tokens can be revoked at once; it outlives the longest token.
        await Db.SetAddAsync(UserIndex(userId), key.ToString());
        await Db.KeyExpireAsync(UserIndex(userId), lifetime);
    }

    public async Task RevokeAllForUserAsync(long userId, CancellationToken ct)
    {
        var members = await Db.SetMembersAsync(UserIndex(userId));
        var keys = members.Select(m => (RedisKey)m.ToString()).Append(UserIndex(userId)).ToArray();
        await Db.KeyDeleteAsync(keys);
    }

    public async Task<long?> ConsumeAsync(string token, CancellationToken ct)
    {
        // GETDEL is atomic, so a token can be consumed by at most one caller.
        var value = await Db.StringGetDeleteAsync(Key(token));
        return value.HasValue && long.TryParse(value.ToString(), out var id) ? id : null;
    }

    public Task RevokeAsync(string token, CancellationToken ct) => Db.KeyDeleteAsync(Key(token));

    private static RedisKey UserIndex(long userId) => $"refresh-user:{userId}";

    private static RedisKey Key(string token) =>
        "refresh:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
