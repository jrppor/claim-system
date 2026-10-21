using Claim.Application.Claims;
using Claim.Domain.Claims;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Claim.Infrastructure.Claims;

public sealed class SqlClaimListQueries(string connectionString) : IClaimListQueries
{
    private sealed record Row(long Id, string ClaimNumber, string Status, string PolicyNumber, string? VehiclePlate,
        string CustomerName, string? CustomerEmail, DateTime CreatedAt, DateTime UpdatedAt);

    public async Task<ClaimPage> SearchAsync(ClaimListQuery query, CancellationToken ct)
    {
        const string from = """
            FROM dbo.Claims c
            JOIN dbo.Policies p ON p.Id = c.PolicyId
            JOIN dbo.Customers cu ON cu.Id = p.CustomerId
            WHERE (@ExcludeDrafts = 0 OR c.Status <> N'Draft')
              AND (@Status IS NULL OR c.Status = @Status)
              AND (@Pattern IS NULL OR c.ClaimNumber LIKE @Pattern ESCAPE N'\' OR c.VehiclePlate LIKE @Pattern ESCAPE N'\'
                   OR cu.FullName LIKE @Pattern ESCAPE N'\' OR cu.Email LIKE @Pattern ESCAPE N'\'
                   OR c.CustomerEmail LIKE @Pattern ESCAPE N'\')
            """;
        var args = new
        {
            query.ExcludeDrafts,
            Status = query.Status?.ToString(),
            Pattern = query.Search is null ? null : "%" + Escape(query.Search) + "%",
            Skip = (query.Page - 1) * query.PageSize,
            Take = query.PageSize,
        };

        await using var conn = new SqlConnection(connectionString);
        var total = await conn.ExecuteScalarAsync<int>(new CommandDefinition($"SELECT COUNT(*) {from}", args, cancellationToken: ct));
        var rows = await conn.QueryAsync<Row>(new CommandDefinition($"""
            SELECT c.Id, c.ClaimNumber, c.Status, p.PolicyNumber, c.VehiclePlate, cu.FullName AS CustomerName,
                   c.CustomerEmail, c.CreatedAt, c.UpdatedAt
            {from}
            ORDER BY c.Id DESC OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY
            """, args, cancellationToken: ct));
        var items = rows.Select(r => new ClaimListItem(r.Id, r.ClaimNumber, r.Status, r.PolicyNumber, r.VehiclePlate,
            r.CustomerName, r.CustomerEmail, r.CreatedAt, r.UpdatedAt)).ToList();
        return new ClaimPage(items, total, query.Page, query.PageSize);
    }

    public async Task<IReadOnlyDictionary<ClaimStatus, int>> CountByStatusAsync(bool excludeDrafts, CancellationToken ct)
    {
        await using var conn = new SqlConnection(connectionString);
        var rows = await conn.QueryAsync<(string Status, int Count)>(new CommandDefinition("""
            SELECT Status, COUNT(*) AS [Count] FROM dbo.Claims
            WHERE @excludeDrafts = 0 OR Status <> N'Draft'
            GROUP BY Status
            """, new { excludeDrafts }, cancellationToken: ct));
        return rows.ToDictionary(r => Enum.Parse<ClaimStatus>(r.Status), r => r.Count);
    }

    private static string Escape(string text) =>
        text.Replace(@"\", @"\\").Replace("[", @"\[").Replace("%", @"\%").Replace("_", @"\_");
}
