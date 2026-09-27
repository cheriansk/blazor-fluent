using BlazorFluent.Jobs.Abstractions;
using BlazorFluent.Jobs.Jobs.Catalog;
using Cronos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Jobs.Schedulers;

/// <summary>
/// Background cron scheduler using Cronos for precise, zero-polling schedule calculation.
/// </summary>
public class PeriodicBatchScheduler : BackgroundService
{
    private readonly IJobEventQueue _queue;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PeriodicBatchScheduler> _logger;

    public PeriodicBatchScheduler(
        IJobEventQueue queue,
        IConfiguration configuration,
        ILogger<PeriodicBatchScheduler> logger)
    {
        _queue = queue;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var cronExpr = _configuration["Jobs:Schedules:CatalogSyncJob"] ?? "*/15 * * * *"; // default: every 15 minutes
        CronExpression expression;

        try
        {
            expression = CronExpression.Parse(cronExpr, CronFormat.Standard);
            _logger.LogInformation("Periodic Batch Scheduler initialized with Cron expression: '{Cron}'", cronExpr);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Invalid Cron expression '{Cron}'. Falling back to default '*/15 * * * *'.", cronExpr);
            expression = CronExpression.Parse("*/15 * * * *", CronFormat.Standard);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var utcNow = DateTime.UtcNow;
            var nextUtc = expression.GetNextOccurrence(utcNow, TimeZoneInfo.Utc);

            if (!nextUtc.HasValue)
            {
                _logger.LogWarning("Cron scheduler found no future occurrences. Stopping scheduler.");
                break;
            }

            var delay = nextUtc.Value - utcNow;
            if (delay > TimeSpan.Zero)
            {
                _logger.LogInformation("Next CatalogSyncJob scheduled at {NextUtc} (in {DelayMinutes:F1} minutes)",
                    nextUtc.Value, delay.TotalMinutes);

                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }

            if (stoppingToken.IsCancellationRequested) break;

            _logger.LogInformation("Cron schedule elapsed. Enqueuing CatalogSyncJobEvent...");
            var jobEvent = new CatalogSyncJobEvent(TriggerSource: "Cron");
            await _queue.EnqueueAsync(jobEvent, stoppingToken);
        }

        _logger.LogInformation("Periodic Batch Scheduler stopped.");
    }
}
