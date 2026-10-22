using Claim.Application.Auth;
using Claim.Application.Health;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        services.AddScoped<Claims.PolicyService>();
        services.AddScoped<Claims.ClaimService>();
        services.AddScoped<Claims.ClaimWorkflowService>();
        services.AddScoped<Claims.ClaimListService>();
        services.AddScoped<Claims.ClaimPhotoService>();
        services.AddScoped<Claims.ClaimPhotoLookup>();
        services.AddScoped<Claims.ClaimPhotoRemovalService>();
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }
}
