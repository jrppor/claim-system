using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Claim.Application.Claims;

/// <summary>
/// Sends the EmailOutbox rows that are due. A failure never throws: the row is rescheduled with exponential
/// backoff so the status change that queued it is unaffected and the Customer is notified once the mail server is back.
/// </summary>
public sealed class EmailOutboxDispatcher(
    IEmailOutboxStore store,
    IEmailSender sender,
    IOptions<OutboxOptions> options,
    TimeProvider clock,
    ILogger<EmailOutboxDispatcher> logger)
{
    /// <summary>Returns how many emails were sent in this pass.</summary>
    public async Task<int> DispatchDueAsync(CancellationToken ct)
    {
        var o = options.Value;
        var due = await store.GetDueAsync(clock.GetUtcNow().UtcDateTime, o.MaxAttempts, o.BatchSize, ct);
        var sent = 0;
        foreach (var row in due)
        {
            try
            {
                await sender.SendAsync(row.Message, ct);
                await store.MarkSentAsync(row.Id, clock.GetUtcNow().UtcDateTime, ct);
                sent++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var attempts = row.Attempts + 1;
                var delay = TimeSpan.FromTicks(Math.Min(o.RetryMaxDelay.Ticks, o.RetryBaseDelay.Ticks << Math.Min(attempts - 1, 20)));
                logger.LogWarning(ex, "Email {OutboxId} for Claim {ClaimId} failed (attempt {Attempts}); retrying in {Delay}",
                    row.Id, row.Message.ClaimId, attempts, delay);
                await store.MarkFailedAsync(row.Id, ex.Message, clock.GetUtcNow().UtcDateTime + delay, ct);
            }
        }
        return sent;
    }
}
