using Claim.Application.Health;
using Microsoft.Data.SqlClient;

namespace Claim.Infrastructure;

public sealed class SqlDatabaseProbe(string connectionString) : IDatabaseProbe
{
    public async Task<bool> IsAvailableAsync(CancellationToken ct)
    {
        try
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync(ct);
            return true;
        }
        catch (SqlException)
        {
            return false;
        }
    }
}
