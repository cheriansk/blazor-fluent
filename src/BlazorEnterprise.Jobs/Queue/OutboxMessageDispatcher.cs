using System.Text.Json;
using BlazorEnterprise.Core.Configuration;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Events;
using BlazorEnterprise.Core.Events;
using BlazorEnterprise.Jobs.Abstractions;
using BlazorEnterprise.Persistence.Context;
using BlazorEnterprise.Persistence.Extensions;
using BlazorEnterprise.Shared.Common;
using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlazorEnterprise.Jobs.Queue;

/// <summary>
/// Transactional Outbox Dispatcher Background Service.
/// Periodically queries unprocessed published events (EventPublishTrackerEntity where Status == Published)
/// that may have been missed due to process crashes or restarts, and safely re-enqueues them into the execution pipeline.
/// </summary>
public class OutboxMessageDispatcher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IJobEventQueue _queue;
    private readonly IOptions<JobsOptions> _options;
    private readonly ILogger<OutboxMessageDispatcher> _logger;

    public OutboxMessageDispatcher(
        IServiceScopeFactory scopeFactory,
        IJobEventQueue queue,
        IOptions<JobsOptions> options,
        ILogger<OutboxMessageDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.EnableOutboxDispatcher)
        {
            _logger.LogInformation("Outbox Message Dispatcher is disabled via configuration.");
            return;
        }

        _logger.LogInformation("Outbox Message Dispatcher started. Polling every {Interval} seconds.",
            _options.Value.OutboxPollingIntervalSeconds);

        var interval = TimeSpan.FromSeconds(Math.Max(5, _options.Value.OutboxPollingIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingOutboxMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in Outbox Message Dispatcher cycle.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ProcessPendingOutboxMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();

        // Recover events older than 15 seconds that are still marked as Published (not picked up by in-memory channel)
        var cutoffTime = DateTime.UtcNow.AddSeconds(-15);
        var batchSize = Math.Max(1, _options.Value.OutboxBatchSize);

        var query = dbContext.EventPublishTrackers
            .Where(e => e.Status == EventTrackerStatusEnum.Published && e.PublishedAtUtc <= cutoffTime)
            .OrderBy(e => e.PublishedAtUtc)
            .Take(batchSize);

        var pendingEvents = await query.ToListWithAuditedBypassAsync(
            auditService,
            "Transactional Outbox recovery of unprocessed events across tenants",
            cancellationToken);

        if (pendingEvents.Count == 0) return;

        _logger.LogInformation("Outbox Dispatcher found {Count} unprocessed published events.", pendingEvents.Count);

        foreach (var record in pendingEvents)
        {
            try
            {
                var eventType = ResolveEventType(record.EventTypeFullName, record.EventName);
                if (eventType == null)
                {
                    _logger.LogWarning("Outbox event {EventId} ('{EventName}') type cannot be resolved. Marking as Failed.",
                        record.EventId, record.EventName);
                    record.Status = EventTrackerStatusEnum.Failed;
                    continue;
                }

                var jobEvent = JsonSerializer.Deserialize(record.PayloadJson, eventType, JsonDefaults.StandardOptions) as IJobEvent;
                if (jobEvent == null)
                {
                    _logger.LogWarning("Outbox event {EventId} ('{EventName}') payload deserialization failed. Marking as Failed.",
                        record.EventId, record.EventName);
                    record.Status = EventTrackerStatusEnum.Failed;
                    continue;
                }

                // Mark in progress in the ledger and re-enqueue
                record.Status = EventTrackerStatusEnum.InProgress;
                await _queue.EnqueueAsync(jobEvent, cancellationToken);

                _logger.LogInformation("Outbox event {EventId} ('{EventName}') successfully recovered and dispatched.",
                    record.EventId, record.EventName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to dispatch outbox event {EventId} ('{EventName}').", record.EventId, record.EventName);
                record.Status = EventTrackerStatusEnum.Failed;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static Type? ResolveEventType(string? eventTypeFullName, string eventName)
    {
        if (!string.IsNullOrWhiteSpace(eventTypeFullName))
        {
            var t = Type.GetType(eventTypeFullName);
            if (t != null) return t;
        }

        // Search assemblies loaded in current AppDomain
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var match = assembly.GetTypes().FirstOrDefault(t =>
                    typeof(IJobEvent).IsAssignableFrom(t) &&
                    (string.Equals(t.Name, eventName, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(t.FullName, eventTypeFullName, StringComparison.OrdinalIgnoreCase)));

                if (match != null) return match;
            }
            catch
            {
                // Ignore assemblies that fail to expose types
            }
        }

        return null;
    }
}
