using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.Events;
using BlazorFluent.Jobs.Abstractions;
using BlazorFluent.Jobs.Jobs.Audit;
using BlazorFluent.Jobs.Jobs.Catalog;
using BlazorFluent.Jobs.Jobs.Tasks;
using BlazorFluent.Persistence.Context;
using Cronos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Jobs.Schedulers;

/// <summary>
/// Multi-job background cron scheduler using Cronos for precise, zero-polling schedule calculation.
/// Iterates over all active tenants to guarantee strict tenant-scoped execution under a configured service principal.
/// </summary>
public class PeriodicBatchScheduler : BackgroundService
{
    private readonly IJobEventQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PeriodicBatchScheduler> _logger;

    private sealed record ScheduleEntry(
        string Name,
        CronExpression Cron,
        Func<string, string, IJobEvent> EventFactory);

    public PeriodicBatchScheduler(
        IJobEventQueue queue,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<PeriodicBatchScheduler> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
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
            var targetTime = nextJob.Value!.Value;
            var delay = targetTime - DateTime.UtcNow;

            // Clamp delay to 24 hours to prevent ArgumentOutOfRangeException on large intervals (> 24.8 days)
            var maxDelay = TimeSpan.FromHours(24);
            var effectiveDelay = delay > maxDelay ? maxDelay : delay;

            if (effectiveDelay > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(effectiveDelay, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }

            if (stoppingToken.IsCancellationRequested) break;

            // If we woke up from a capped intermediate delay before targetTime, re-evaluate loop
            if (DateTime.UtcNow < targetTime)
            {
                continue;
            }

            var targetSchedule = schedules.FirstOrDefault(s => s.Name == nextJob.Key);
            if (targetSchedule != null)
            {
                var cronServiceEmail = _configuration["Jobs:CronServiceAccountEmail"] ?? "cron-daemon@blazorfluent.local";

                List<string> activeTenants = [];
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    activeTenants = await db.Tenants
                        .AsNoTracking()
                        .Where(t => t.IsActive)
                        .Select(t => t.Slug)
                        .ToListAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to query active tenants for cron job '{JobName}'.", targetSchedule.Name);
                }

                if (activeTenants.Count == 0)
                {
                    activeTenants.Add(IRootAdminService.DefaultTenantSlug);
                }

                _logger.LogInformation(
                    "Cron schedule elapsed for '{JobName}'. Dispatching for {TenantCount} active tenant(s)...",
                    targetSchedule.Name, activeTenants.Count);

                foreach (var tenantId in activeTenants)
                {
                    var jobEvent = targetSchedule.EventFactory(tenantId, cronServiceEmail);
                    await _queue.EnqueueAsync(jobEvent, stoppingToken);
                }

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
                (tenantId, email) => new CatalogSyncJobEvent(
                    TriggerSource: "Cron",
                    TenantId: tenantId,
                    CorrelationId: Guid.CreateVersion7().ToString("N")[..12],
                    SenderOrigin: "CatalogSyncJob",
                    SenderUserId: "cron:CatalogSyncJob",
                    SenderUserEmail: email)));
        }

        var auditPurgeCron = _configuration["Jobs:Schedules:AuditPurgeJob"] ?? "0 2 * * *";
        var retentionDays = int.TryParse(_configuration["Audit:RetentionDays"], out var days) ? days : 365;
        if (TryParseCron("AuditPurgeJob", auditPurgeCron, out var parsedPurge))
        {
            entries.Add(new ScheduleEntry(
                "AuditPurgeJob",
                parsedPurge,
                (tenantId, email) => new AuditPurgeJobEvent(
                    TriggerSource: "Cron",
                    TenantId: tenantId,
                    CorrelationId: Guid.CreateVersion7().ToString("N")[..12],
                    SenderOrigin: "AuditPurgeJob",
                    SenderUserId: "cron:AuditPurgeJob",
                    SenderUserEmail: email,
                    RetentionDays: retentionDays)));
        }

        var dailyTaskCron = _configuration["Jobs:Schedules:DailyTaskSummaryJob"] ?? "0 18 * * *";
        if (TryParseCron("DailyTaskSummaryJob", dailyTaskCron, out var parsedTaskCron))
        {
            entries.Add(new ScheduleEntry(
                "DailyTaskSummaryJob",
                parsedTaskCron,
                (tenantId, email) => new DailyTaskSummaryJobEvent(
                    TriggerSource: "Cron",
                    TenantId: tenantId,
                    CorrelationId: Guid.CreateVersion7().ToString("N")[..12],
                    SenderOrigin: "DailyTaskSummaryJob",
                    SenderUserId: "cron:DailyTaskSummaryJob",
                    SenderUserEmail: email)));
        }

        var cadenceCron = _configuration["Jobs:Schedules:TaskCadenceAlertJob"] ?? "0 8 * * *";
        if (TryParseCron("TaskCadenceAlertJob", cadenceCron, out var parsedCadenceCron))
        {
            entries.Add(new ScheduleEntry(
                "TaskCadenceAlertJob",
                parsedCadenceCron,
                (tenantId, email) => new TaskCadenceAlertJobEvent(
                    TriggerSource: "Cron",
                    TenantId: tenantId,
                    CorrelationId: Guid.CreateVersion7().ToString("N")[..12],
                    SenderOrigin: "TaskCadenceAlertJob",
                    SenderUserId: "cron:TaskCadenceAlertJob",
                    SenderUserEmail: email)));
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
