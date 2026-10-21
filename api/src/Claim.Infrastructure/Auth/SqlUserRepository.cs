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

    public async Task<User?> CreateAsync(string email, string passwordHash, string displayName, string role, CancellationToken ct)
    {
        await using var conn = new SqlConnection(connectionString);
        try
        {
            return await conn.QuerySingleAsync<User>(new CommandDefinition(
                $"INSERT INTO dbo.Users (Email, PasswordHash, DisplayName, Role) OUTPUT INSERTED.Id, INSERTED.Email, INSERTED.PasswordHash, INSERTED.DisplayName, INSERTED.Role, INSERTED.IsActive VALUES (@email, @passwordHash, @displayName, @role)",
                new { email, passwordHash, displayName, role }, cancellationToken: ct));
        }
        catch (SqlException ex) when (ex.Number is 2627 or 2601) // unique constraint / index violation
        {
            return null;
        }
    }

    public async Task<bool> SetPasswordHashAsync(long id, string passwordHash, CancellationToken ct)
    {
        await using var conn = new SqlConnection(connectionString);
        var rows = await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE dbo.Users SET PasswordHash = @passwordHash WHERE Id = @id", new { id, passwordHash }, cancellationToken: ct));
        return rows > 0;
    }

    public async Task<bool> SetActiveAsync(long id, bool isActive, CancellationToken ct)
    {
        await using var conn = new SqlConnection(connectionString);
        var rows = await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE dbo.Users SET IsActive = @isActive WHERE Id = @id", new { id, isActive }, cancellationToken: ct));
        return rows > 0;
    }
}
