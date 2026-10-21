using Claim.Application.Claims;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Claim.Infrastructure.Claims;

public sealed class SqlEmailOutboxStore(string connectionString) : IEmailOutboxStore
{
    private sealed record Row(long Id, long ClaimId, string ToAddress, string Subject, string Body,
        DateTime CreatedAt, int Attempts, DateTime NextAttemptAt, DateTime? SentAt, string? LastError);

    public async Task<IReadOnlyList<OutboxEmail>> GetDueAsync(DateTime now, int maxAttempts, int batchSize, CancellationToken ct)
    {
        await using var conn = new SqlConnection(connectionString);
        var rows = await conn.QueryAsync<Row>(new CommandDefinition(
            """
            SELECT TOP (@batchSize) Id, ClaimId, ToAddress, Subject, Body, CreatedAt, Attempts, NextAttemptAt, SentAt, LastError
            FROM dbo.EmailOutbox
            WHERE SentAt IS NULL AND Attempts < @maxAttempts AND NextAttemptAt <= @now
            ORDER BY Id
            """,
            new { now, maxAttempts, batchSize }, cancellationToken: ct));
        return rows.Select(r => new OutboxEmail(
            r.Id, new EmailMessage(r.ClaimId, r.ToAddress, r.Subject, r.Body),
            r.CreatedAt, r.Attempts, r.NextAttemptAt, r.SentAt, r.LastError)).ToList();
    }

    public async Task MarkSentAsync(long id, DateTime sentAt, CancellationToken ct)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE dbo.EmailOutbox SET SentAt = @sentAt, Attempts = Attempts + 1, LastError = NULL WHERE Id = @id",
            new { id, sentAt }, cancellationToken: ct));
    }

    public async Task MarkFailedAsync(long id, string error, DateTime nextAttemptAt, CancellationToken ct)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE dbo.EmailOutbox SET Attempts = Attempts + 1, LastError = LEFT(@error, 2000), NextAttemptAt = @nextAttemptAt WHERE Id = @id",
            new { id, error, nextAttemptAt }, cancellationToken: ct));
    }
}
