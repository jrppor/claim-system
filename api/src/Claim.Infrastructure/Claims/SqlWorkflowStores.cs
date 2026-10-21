using Claim.Application.Claims;
using ClaimEntity = Claim.Domain.Claims.Claim;
using Claim.Domain.Claims;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Claim.Infrastructure.Claims;

public sealed class SqlClaimHistoryRepository(string connectionString) : IClaimHistoryRepository
{
    private sealed record Row(long ClaimId, string? FromStatus, string ToStatus, long ActorUserId, DateTime OccurredAt, string? Reason);

    public async Task<IReadOnlyList<ClaimHistoryEntry>> ListAsync(long claimId, CancellationToken ct)
    {
        await using var conn = new SqlConnection(connectionString);
        var rows = await conn.QueryAsync<Row>(new CommandDefinition(
            "SELECT ClaimId, FromStatus, ToStatus, ActorUserId, OccurredAt, Reason FROM dbo.ClaimHistory WHERE ClaimId = @claimId ORDER BY Id",
            new { claimId }, cancellationToken: ct));
        return rows.Select(r => new ClaimHistoryEntry(
            r.ClaimId, r.FromStatus is null ? null : Enum.Parse<ClaimStatus>(r.FromStatus), Enum.Parse<ClaimStatus>(r.ToStatus),
            r.ActorUserId, r.OccurredAt, r.Reason)).ToList();
    }
}

/// <summary>One SQL transaction per status change: Claims row, ClaimHistory row (and the EmailOutbox row) commit together.</summary>
public sealed class SqlUnitOfWork(string connectionString) : IUnitOfWork
{
    public async Task RecordStatusChangeAsync(ClaimEntity claim, ClaimHistoryEntry entry, EmailMessage? email, CancellationToken ct)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(ct);

        await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE dbo.Claims SET Status = @Status, UpdatedAt = @UpdatedAt WHERE Id = @Id",
            new { claim.Id, Status = claim.Status.ToString(), claim.UpdatedAt }, tx, cancellationToken: ct));

        await conn.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO dbo.ClaimHistory (ClaimId, FromStatus, ToStatus, ActorUserId, OccurredAt, Reason)
            VALUES (@ClaimId, @FromStatus, @ToStatus, @ActorUserId, @OccurredAt, @Reason)
            """,
            new
            {
                entry.ClaimId, FromStatus = entry.FromStatus?.ToString(), ToStatus = entry.ToStatus.ToString(),
                entry.ActorUserId, entry.OccurredAt, entry.Reason,
            }, tx, cancellationToken: ct));

        if (email is not null)
            await conn.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO dbo.EmailOutbox (ClaimId, ToAddress, Subject, Body, CreatedAt, NextAttemptAt)
                VALUES (@ClaimId, @ToAddress, @Subject, @Body, @now, @now)
                """,
                new { email.ClaimId, email.ToAddress, email.Subject, email.Body, now = entry.OccurredAt }, tx, cancellationToken: ct));

        await tx.CommitAsync(ct);
    }
}
