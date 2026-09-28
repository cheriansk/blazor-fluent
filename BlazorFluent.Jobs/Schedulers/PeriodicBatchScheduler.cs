using BlazorFluent.Core.Events;
using BlazorFluent.Jobs.Abstractions;
using BlazorFluent.Jobs.Jobs.Audit;
using BlazorFluent.Jobs.Jobs.Catalog;
using Cronos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Jobs.Schedulers;

/// <summary>
/// Multi-job background cron scheduler using Cronos for precise, zero-polling schedule calculation.
/// </summary>
public class PeriodicBatchScheduler : BackgroundService
{
    private readonly IJobEventQueue _queue;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PeriodicBatchScheduler> _logger;

    private sealed record ScheduleEntry(
        string Name,
        CronExpression Cron,
        Func<IJobEvent> EventFactory);

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
        var schedules = BuildSchedules();
        if (schedules.Count == 0)
        {
            _logger.LogWarning("No periodic schedules configured. PeriodicBatchScheduler stopping.");
            return;
        }

        var nextFires = new Dictionary<string, DateTime?>();
        foreach (var s in schedules)
        {
            nextFires[s.Name] = s.Cron.GetNextOccurrence(DateTime.UtcNow, TimeZoneInfo.Utc);
            _logger.LogInformation("Scheduled job '{JobName}' initialized. Next fire: {NextUtc}", s.Name, nextFires[s.Name]);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var upcoming = nextFires
                .Where(kv => kv.Value.HasValue)
                .OrderBy(kv => kv.Value!.Value)
                .ToList();

            if (upcoming.Count == 0)
            {
                _logger.LogWarning("Cron scheduler found no future occurrences for any job. Stopping scheduler.");
                break;
            }

            var nextJob = upcoming.First();
            var delay = nextJob.Value!.Value - DateTime.UtcNow;

            if (delay > TimeSpan.Zero)
            {
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

            var targetSchedule = schedules.FirstOrDefault(s => s.Name == nextJob.Key);
            if (targetSchedule != null)
            {
                _logger.LogInformation("Cron schedule elapsed for '{JobName}'. Enqueuing event...", targetSchedule.Name);
                await _queue.EnqueueAsync(targetSchedule.EventFactory(), stoppingToken);
                nextFires[targetSchedule.Name] = targetSchedule.Cron.GetNextOccurrence(DateTime.UtcNow, TimeZoneInfo.Utc);
            }
            else
            {
                nextFires.Remove(nextJob.Key);
            }
        }

        _logger.LogInformation("Periodic Batch Scheduler stopped.");
    }

    private List<ScheduleEntry> BuildSchedules()
    {
        var entries = new List<ScheduleEntry>();

        var catalogCron = _configuration["Jobs:Schedules:CatalogSyncJob"] ?? "*/15 * * * *";
        if (TryParseCron("CatalogSyncJob", catalogCron, out var parsedCatalog))
        {
            entries.Add(new ScheduleEntry(
                "CatalogSyncJob",
                parsedCatalog,
                () => new CatalogSyncJobEvent(TriggerSource: "Cron")));
        }

        var auditPurgeCron = _configuration["Jobs:Schedules:AuditPurgeJob"] ?? "0 2 * * *";
        var retentionDays = int.TryParse(_configuration["Audit:RetentionDays"], out var days) ? days : 365;
        if (TryParseCron("AuditPurgeJob", auditPurgeCron, out var parsedPurge))
        {
            entries.Add(new ScheduleEntry(
                "AuditPurgeJob",
                parsedPurge,
                () => new AuditPurgeJobEvent(TriggerSource: "Cron", RetentionDays: retentionDays)));
        }

        return entries;
    }

    private bool TryParseCron(string jobName, string expression, out CronExpression cron)
    {
        try
        {
            cron = CronExpression.Parse(expression, CronFormat.Standard);
            _logger.LogInformation("Job '{JobName}' registered with cron '{Expression}'", jobName, expression);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Invalid cron expression '{Expression}' for job '{JobName}'", expression, jobName);
            cron = null!;
            return false;
        }
    }
}
