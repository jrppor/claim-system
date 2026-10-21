using Claim.Application.Auth;
using Claim.Domain.Users;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Claim.Infrastructure.Auth;

public sealed class SqlUserRepository(string connectionString) : IUserRepository
{
    private const string Columns = "Id, Email, PasswordHash, DisplayName, Role, IsActive";

    public async Task<User?> FindByEmailAsync(string email, CancellationToken ct)
    {
        await using var conn = new SqlConnection(connectionString);
        return await conn.QuerySingleOrDefaultAsync<User>(new CommandDefinition(
            $"SELECT {Columns} FROM dbo.Users WHERE Email = @email", new { email }, cancellationToken: ct));
    }

    public async Task<User?> FindByIdAsync(long id, CancellationToken ct)
    {
        await using var conn = new SqlConnection(connectionString);
        return await conn.QuerySingleOrDefaultAsync<User>(new CommandDefinition(
            $"SELECT {Columns} FROM dbo.Users WHERE Id = @id", new { id }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<User>> ListAsync(CancellationToken ct)
    {
        await using var conn = new SqlConnection(connectionString);
        var rows = await conn.QueryAsync<User>(new CommandDefinition(
            $"SELECT {Columns} FROM dbo.Users ORDER BY Id", cancellationToken: ct));
        return rows.ToList();
    }
}
