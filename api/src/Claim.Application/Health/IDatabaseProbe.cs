namespace Claim.Application.Health;

/// <summary>Port: lets the health use case ask whether the database is reachable.</summary>
public interface IDatabaseProbe
{
    Task<bool> IsAvailableAsync(CancellationToken ct);
}
