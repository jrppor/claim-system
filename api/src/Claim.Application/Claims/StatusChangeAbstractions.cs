using Claim.Domain.Claims;

namespace Claim.Application.Claims;

/// <summary>
/// Persists a status change atomically: the Claim's new status and its Claim History line and EmailOutbox row (ADR 0002) commit
/// in one transaction or not at all. Later lifecycle tickets reuse it for every transition.
/// </summary>
public interface IUnitOfWork
{
    Task RecordStatusChangeAsync(Domain.Claims.Claim claim, ClaimHistoryEntry entry, EmailMessage? email, CancellationToken ct);

    /// <summary>
    /// Stores a new Repair Estimate revision (the store assigns its number: previous latest + 1; earlier revisions are never touched).
    /// When the submission also moves the Claim, the new status, History line and email commit in the same transaction.
    /// </summary>
    Task<RepairEstimate> RecordEstimateAsync(
        RepairEstimate estimate, Domain.Claims.Claim? claim, ClaimHistoryEntry? entry, EmailMessage? email, CancellationToken ct);

    /// <summary>Stores the Invoice and moves the Claim to InvoiceSubmitted; Invoice, status, History line and email commit together.</summary>
    Task<Invoice> RecordInvoiceAsync(Invoice invoice, Domain.Claims.Claim claim, ClaimHistoryEntry entry, EmailMessage? email, CancellationToken ct);

    /// <summary>
    /// Stores Claim Photo rows, each with its Claim History line, all in one transaction: either every photo is saved or none.
    /// The Claim's status does not change and no email is queued, but the Claim row is written (version compared and refreshed)
    /// so a stale caller is refused and the photo change is a new version. <paramref name="entries"/> pairs with <paramref name="photos"/> by index.
    /// </summary>
    Task<IReadOnlyList<ClaimPhoto>> RecordPhotosAsync(
        IReadOnlyList<ClaimPhoto> photos, Domain.Claims.Claim claim, IReadOnlyList<ClaimHistoryEntry> entries, CancellationToken ct);
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
