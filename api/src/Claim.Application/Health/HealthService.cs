using Microsoft.Extensions.Options;

namespace Claim.Application.Health;

public sealed record HealthReport(string Status, string Database, string GarageName, string InsurerName)
{
    public bool IsHealthy => Status == "Healthy";
}

public sealed class OwnerOptions
{
    public const string Section = "Owner";
    public string GarageName { get; set; } = "";
    public string InsurerName { get; set; } = "";
}

public sealed class HealthService(IDatabaseProbe database, IOptions<OwnerOptions> owner)
{
    public async Task<HealthReport> CheckAsync(CancellationToken ct)
    {
        var up = await database.IsAvailableAsync(ct);
        return new HealthReport(up ? "Healthy" : "Unhealthy", up ? "Up" : "Down",
            owner.Value.GarageName, owner.Value.InsurerName);
    }
}
