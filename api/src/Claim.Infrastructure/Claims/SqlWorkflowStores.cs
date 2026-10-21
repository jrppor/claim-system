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

public sealed class SqlRepairEstimateRepository(string connectionString) : IRepairEstimateRepository
{
    private sealed record EstimateRow(long Id, long ClaimId, int Revision, long SubmittedByUserId, DateTime SubmittedAt);
    private sealed record LineRow(long RepairEstimateId, string Kind, string Description, decimal Quantity, decimal UnitPrice);

    public async Task<IReadOnlyList<RepairEstimate>> ListAsync(long claimId, CancellationToken ct)
    {
        await using var conn = new SqlConnection(connectionString);
        var rows = (await conn.QueryAsync<EstimateRow>(new CommandDefinition(
            "SELECT Id, ClaimId, Revision, SubmittedByUserId, SubmittedAt FROM dbo.RepairEstimates WHERE ClaimId = @claimId ORDER BY Revision",
            new { claimId }, cancellationToken: ct))).ToList();
        var lines = (await conn.QueryAsync<LineRow>(new CommandDefinition(
            """
            SELECT l.RepairEstimateId, l.Kind, l.Description, l.Quantity, l.UnitPrice
            FROM dbo.RepairEstimateLines l JOIN dbo.RepairEstimates e ON e.Id = l.RepairEstimateId
            WHERE e.ClaimId = @claimId ORDER BY l.RepairEstimateId, l.LineNumber
            """, new { claimId }, cancellationToken: ct))).ToLookup(l => l.RepairEstimateId);
        return rows.Select(r => new RepairEstimate(lines[r.Id]
                .Select(l => new EstimateLine(Enum.Parse<EstimateLineKind>(l.Kind), l.Description, l.Quantity, l.UnitPrice)).ToList())
            { Id = r.Id, ClaimId = r.ClaimId, Revision = r.Revision, SubmittedByUserId = r.SubmittedByUserId, SubmittedAt = r.SubmittedAt }).ToList();
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
        await WriteStatusChangeAsync(conn, tx, claim, entry, email, ct);
        await tx.CommitAsync(ct);
    }

    public async Task<RepairEstimate> RecordEstimateAsync(
        RepairEstimate estimate, ClaimEntity? claim, ClaimHistoryEntry? entry, EmailMessage? email, CancellationToken ct)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(ct);

        // Next revision is read under UPDLOCK/HOLDLOCK so two concurrent submissions cannot take the same number
        // (UQ_RepairEstimates_ClaimRevision is the backstop).
        var revision = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT ISNULL(MAX(Revision), 0) + 1 FROM dbo.RepairEstimates WITH (UPDLOCK, HOLDLOCK) WHERE ClaimId = @ClaimId",
            new { estimate.ClaimId }, tx, cancellationToken: ct));
        var id = await conn.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            INSERT INTO dbo.RepairEstimates (ClaimId, Revision, EstimatedAmount, SubmittedByUserId, SubmittedAt)
            OUTPUT INSERTED.Id
            VALUES (@ClaimId, @revision, @EstimatedAmount, @SubmittedByUserId, @SubmittedAt)
            """,
            new { estimate.ClaimId, revision, estimate.EstimatedAmount, estimate.SubmittedByUserId, estimate.SubmittedAt }, tx, cancellationToken: ct));
        var lineNumber = 0;
        foreach (var line in estimate.Lines)
            await conn.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO dbo.RepairEstimateLines (RepairEstimateId, LineNumber, Kind, Description, Quantity, UnitPrice, LineTotal)
                VALUES (@id, @lineNumber, @Kind, @Description, @Quantity, @UnitPrice, @LineTotal)
                """,
                new { id, lineNumber = ++lineNumber, Kind = line.Kind.ToString(), line.Description, line.Quantity, line.UnitPrice, line.LineTotal },
                tx, cancellationToken: ct));

        if (claim is not null && entry is not null) await WriteStatusChangeAsync(conn, tx, claim, entry, email, ct);
        await tx.CommitAsync(ct);
        return estimate.Stored(id, revision);
    }

    private static async Task WriteStatusChangeAsync(
        SqlConnection conn, SqlTransaction tx, ClaimEntity claim, ClaimHistoryEntry entry, EmailMessage? email, CancellationToken ct)
    {
        await conn.ExecuteAsync(new CommandDefinition(
            """
            UPDATE dbo.Claims
            SET Status = @Status, UpdatedAt = @UpdatedAt, ApprovedAmount = @ApprovedAmount, ApprovedRepairEstimateId = @ApprovedEstimateId
            WHERE Id = @Id
            """,
            new { claim.Id, Status = claim.Status.ToString(), claim.UpdatedAt, claim.ApprovedAmount, claim.ApprovedEstimateId }, tx, cancellationToken: ct));

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
    }
}
