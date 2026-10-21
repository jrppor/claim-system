using Claim.Application.Auth;
using Claim.Application.Health;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Claim.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<OwnerOptions>(config.GetSection(OwnerOptions.Section));
        services.Configure<AuthOptions>(config.GetSection(AuthOptions.Section));
        services.AddScoped<HealthService>();
        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        return services;
    }
}
