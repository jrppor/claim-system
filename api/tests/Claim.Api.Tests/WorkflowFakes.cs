using Claim.Application.Claims;
using Claim.Domain.Claims;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Claim.Api.Tests;

/// <summary>In-memory Claim History. Also what the fake Unit of Work appends to.</summary>
public sealed class FakeClaimHistoryRepository : IClaimHistoryRepository
{
    private readonly List<ClaimHistoryEntry> _entries = [];
    private readonly object _lock = new();

    public IReadOnlyList<ClaimHistoryEntry> All { get { lock (_lock) return _entries.ToList(); } }

    public void Append(ClaimHistoryEntry entry)
    {
        lock (_lock) _entries.Add(entry);
    }

    public Task<IReadOnlyList<ClaimHistoryEntry>> ListAsync(long claimId, CancellationToken ct)
    {
        lock (_lock) return Task.FromResult<IReadOnlyList<ClaimHistoryEntry>>(_entries.Where(e => e.ClaimId == claimId).ToList());
    }
}

/// <summary>In-memory EmailOutbox: tests inspect <see cref="Messages"/>; the background sender uses the store interface.</summary>
public sealed class FakeEmailOutbox : IEmailOutboxStore
{
    private readonly List<OutboxEmail> _rows = [];
    private readonly object _lock = new();

    public IReadOnlyList<OutboxEmail> Messages { get { lock (_lock) return _rows.ToList(); } }

    public void Add(EmailMessage message, DateTime now)
    {
        lock (_lock) _rows.Add(new OutboxEmail(_rows.Count + 1, message, now, 0, now, null, null));
    }

    public Task<IReadOnlyList<OutboxEmail>> GetDueAsync(DateTime now, int maxAttempts, int batchSize, CancellationToken ct)
    {
        lock (_lock)
            return Task.FromResult<IReadOnlyList<OutboxEmail>>(_rows
                .Where(r => r.SentAt is null && r.Attempts < maxAttempts && r.NextAttemptAt <= now)
                .Take(batchSize).ToList());
    }

    public Task MarkSentAsync(long id, DateTime sentAt, CancellationToken ct)
    {
        Update(id, r => r with { Attempts = r.Attempts + 1, SentAt = sentAt, LastError = null });
        return Task.CompletedTask;
    }

    public Task MarkFailedAsync(long id, string error, DateTime nextAttemptAt, CancellationToken ct)
    {
        Update(id, r => r with { Attempts = r.Attempts + 1, LastError = error, NextAttemptAt = nextAttemptAt });
        return Task.CompletedTask;
    }

    private void Update(long id, Func<OutboxEmail, OutboxEmail> change)
    {
        lock (_lock)
        {
            var i = _rows.FindIndex(r => r.Id == id);
            _rows[i] = change(_rows[i]);
        }
    }
}

/// <summary>Stands in for the mail server. Set <see cref="IsDown"/> to simulate SMTP being unreachable.</summary>
public sealed class FakeEmailSender : IEmailSender
{
    private readonly List<EmailMessage> _sent = [];
    private readonly object _lock = new();

    public bool IsDown { get; set; }
    public IReadOnlyList<EmailMessage> Sent { get { lock (_lock) return _sent.ToList(); } }

    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        if (IsDown) throw new InvalidOperationException("SMTP is unreachable");
        lock (_lock) _sent.Add(message);
        return Task.CompletedTask;
    }
}

/// <summary>In-memory Repair Estimates. Also what the fake Unit of Work appends to.</summary>
public sealed class FakeRepairEstimateRepository : IRepairEstimateRepository
{
    private readonly List<RepairEstimate> _estimates = [];
    private readonly object _lock = new();

    public IReadOnlyList<RepairEstimate> All { get { lock (_lock) return _estimates.ToList(); } }

    public RepairEstimate Append(RepairEstimate estimate)
    {
        lock (_lock)
        {
            var revision = _estimates.Where(e => e.ClaimId == estimate.ClaimId).Select(e => e.Revision).DefaultIfEmpty(0).Max() + 1;
            var stored = estimate.Stored(_estimates.Count + 1, revision);
            _estimates.Add(stored);
            return stored;
        }
    }

    public Task<IReadOnlyList<RepairEstimate>> ListAsync(long claimId, CancellationToken ct)
    {
        lock (_lock) return Task.FromResult<IReadOnlyList<RepairEstimate>>(_estimates.Where(e => e.ClaimId == claimId).OrderBy(e => e.Revision).ToList());
    }
}

public sealed class FakeUnitOfWork(
    FakeClaimRepository claims, FakeClaimHistoryRepository history, FakeEmailOutbox outbox, FakeRepairEstimateRepository estimates) : IUnitOfWork
{
    public async Task<RepairEstimate> RecordEstimateAsync(
        RepairEstimate estimate, Domain.Claims.Claim? claim, ClaimHistoryEntry? entry, EmailMessage? email, CancellationToken ct)
    {
        if (claim is not null && entry is not null) await RecordStatusChangeAsync(claim, entry, email, ct);
        return estimates.Append(estimate);
    }

    public async Task RecordStatusChangeAsync(Domain.Claims.Claim claim, ClaimHistoryEntry entry, EmailMessage? email, CancellationToken ct)
    {
        await claims.UpdateAsync(claim, ct);
        history.Append(entry);
        if (email is not null) outbox.Add(email, entry.OccurredAt);
    }
}

public static class WorkflowFakeRegistration
{
    public static IServiceCollection ReplaceWorkflowStores(
        this IServiceCollection services, FakeClaimHistoryRepository history, FakeUnitOfWork unitOfWork, FakeEmailOutbox outbox, FakeEmailSender sender, FakeRepairEstimateRepository estimates)
    {
        services.RemoveAll<IRepairEstimateRepository>();
        services.AddSingleton<IRepairEstimateRepository>(estimates);
        services.RemoveAll<IClaimHistoryRepository>();
        services.AddSingleton<IClaimHistoryRepository>(history);
        services.RemoveAll<IEmailSender>();
        services.AddSingleton<IEmailSender>(sender);
        services.Configure<OutboxOptions>(o =>
        {
            o.PollInterval = TimeSpan.FromMilliseconds(20);
            o.RetryBaseDelay = TimeSpan.FromMilliseconds(40);
            o.RetryMaxDelay = TimeSpan.FromMilliseconds(200);
        });
        services.RemoveAll<IEmailOutboxStore>();
        services.AddSingleton<IEmailOutboxStore>(outbox);
        services.RemoveAll<IUnitOfWork>();
        services.AddSingleton<IUnitOfWork>(unitOfWork);
        return services;
    }
}
