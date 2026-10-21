using Claim.Application.Claims;
using Microsoft.Extensions.Options;

namespace Claim.Api.Email;

/// <summary>Background loop that drains the EmailOutbox (ADR 0002).</summary>
public sealed class OutboxWorker(EmailOutboxDispatcher dispatcher, IOptions<OutboxOptions> options, ILogger<OutboxWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await dispatcher.DispatchDueAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // e.g. the database is briefly unavailable; the next poll tries again.
                logger.LogError(ex, "Email outbox pass failed");
            }
            try
            {
                await Task.Delay(options.Value.PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}

public static class OutboxWorkerRegistration
{
    public static IServiceCollection AddEmailOutboxWorker(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<OutboxOptions>(config.GetSection(OutboxOptions.Section));
        services.AddSingleton<EmailOutboxDispatcher>();
        services.AddHostedService<OutboxWorker>();
        return services;
    }
}
