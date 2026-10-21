using Claim.Application.Health;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Claim.Api.Tests;

public sealed class FakeDatabaseProbe : IDatabaseProbe
{
    public bool IsUp { get; set; } = true;
    public bool Throws { get; set; }

    public Task<bool> IsAvailableAsync(CancellationToken ct)
    {
        if (Throws) throw new InvalidOperationException("boom: secret connection detail");
        return Task.FromResult(IsUp);
    }
}

/// <summary>Test host: real pipeline, in-memory fakes in place of infrastructure.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public FakeDatabaseProbe Database { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDatabaseProbe>();
            services.AddSingleton<IDatabaseProbe>(Database);
        });
    }
}
