using Claim.Application.Auth;
using Claim.Application.Health;
using Claim.Infrastructure.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Claim.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var cs = config.GetConnectionString("Claim")
            ?? throw new InvalidOperationException("ConnectionStrings:Claim is not configured.");
        services.AddSingleton<IDatabaseProbe>(new SqlDatabaseProbe(cs));
        services.AddSingleton<IUserRepository>(new SqlUserRepository(cs));
        services.AddSingleton<Application.Claims.IPolicyRepository>(new Claims.SqlPolicyRepository(cs));
        services.AddSingleton<Application.Claims.IClaimRepository>(new Claims.SqlClaimRepository(cs));
        services.AddSingleton<Application.Claims.IClaimHistoryRepository>(new Claims.SqlClaimHistoryRepository(cs));
        services.AddSingleton<Application.Claims.IUnitOfWork>(new Claims.SqlUnitOfWork(cs));
        services.AddSingleton<Application.Claims.IEmailOutboxStore>(new Claims.SqlEmailOutboxStore(cs));
        services.AddSingleton<Application.Claims.IEmailSender, Email.SmtpEmailSender>();

        // Connect lazily so the API starts (and tests run) without Redis.
        var redisConfig = config["Redis:Configuration"] ?? "localhost:6379";
        services.AddSingleton(new Lazy<IConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(redisConfig)));
        services.AddSingleton<IRefreshTokenStore, RedisRefreshTokenStore>();

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        return services;
    }
}
