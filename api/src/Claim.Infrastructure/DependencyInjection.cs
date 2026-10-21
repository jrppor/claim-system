using Claim.Application.Health;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Claim.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var cs = config.GetConnectionString("Claim")
            ?? throw new InvalidOperationException("ConnectionStrings:Claim is not configured.");
        services.AddSingleton<IDatabaseProbe>(new SqlDatabaseProbe(cs));
        return services;
    }
}
