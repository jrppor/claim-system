using Claim.Application.Health;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Claim.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<OwnerOptions>(config.GetSection(OwnerOptions.Section));
        services.AddScoped<HealthService>();
        return services;
    }
}
