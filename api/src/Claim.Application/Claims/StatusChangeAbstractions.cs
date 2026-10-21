using Claim.Domain.Claims;

namespace Claim.Application.Claims;

/// <summary>
/// Persists a status change atomically: the Claim's new status and its Claim History line and EmailOutbox row (ADR 0002) commit
/// in one transaction or not at all. Later lifecycle tickets reuse it for every transition.
/// </summary>
public interface IUnitOfWork
{
    Task RecordStatusChangeAsync(Domain.Claims.Claim claim, ClaimHistoryEntry entry, EmailMessage? email, CancellationToken ct);
}

public interface IClaimHistoryRepository
{
    /// <summary>History of one Claim, oldest first.</summary>
    Task<IReadOnlyList<ClaimHistoryEntry>> ListAsync(long claimId, CancellationToken ct);
}

/// <summary>An email to be sent to a Customer.</summary>
public sealed record EmailMessage(long ClaimId, string ToAddress, string Subject, string Body);

/// <summary>An EmailOutbox row: the message plus its delivery state (ADR 0002).</summary>
public sealed record OutboxEmail(
    long Id,
    EmailMessage Message,
    DateTime CreatedAt,
    int Attempts,
    DateTime NextAttemptAt,
    DateTime? SentAt,
    string? LastError);

/// <summary>Read/update side of the EmailOutbox, used by the background sender. Rows are inserted only through <see cref="IUnitOfWork"/>.</summary>
public interface IEmailOutboxStore
{
    /// <summary>Unsent rows that are due and have attempts left, oldest first.</summary>
    Task<IReadOnlyList<OutboxEmail>> GetDueAsync(DateTime now, int maxAttempts, int batchSize, CancellationToken ct);
    Task MarkSentAsync(long id, DateTime sentAt, CancellationToken ct);
    Task MarkFailedAsync(long id, string error, DateTime nextAttemptAt, CancellationToken ct);
}

/// <summary>Sends one email through the real mail server (SMTP/Mailpit).</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct);
}

public sealed record ClaimHistoryDto(
    string? FromStatus,
    string ToStatus,
    long ActorUserId,
    string ActorName,
    DateTime OccurredAt,
    string? Reason);
