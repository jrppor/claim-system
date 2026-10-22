using Claim.Application.Health;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Claim.Infrastructure;

public sealed class SqlDatabaseProbe(string connectionString, ILogger<SqlDatabaseProbe> logger) : IDatabaseProbe
{
    public async Task<bool> IsAvailableAsync(CancellationToken ct)
    {
        try
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync(ct);
            return true;
        }
        catch (SqlException ex)
        {
            // "Unavailable" is the answer, but the reason (login failed, timeout, wrong server) is what an operator needs.
            logger.LogWarning(ex, "Database health probe failed");
            return false;
        }
    }
}
