namespace Claim.Application.Claims;

/// <summary>How the background sender drains the EmailOutbox (configuration section "Outbox").</summary>
public sealed class OutboxOptions
{
    public const string Section = "Outbox";

    /// <summary>How often the sender looks for due rows.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Wait after the first failure; doubles on every further failure of the same row.</summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(10);

    public TimeSpan RetryMaxDelay { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>A row that failed this many times is left alone (visible in the table with its LastError).</summary>
    public int MaxAttempts { get; set; } = 10;

    public int BatchSize { get; set; } = 20;
}
