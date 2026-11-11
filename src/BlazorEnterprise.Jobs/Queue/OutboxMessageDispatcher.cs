using System.Text.Json;
using BlazorEnterprise.Core.Configuration;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Events;
using BlazorEnterprise.Core.Events;
using BlazorEnterprise.Core.Utilities;
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
        // 1. Read-only discovery pass across tenants using App-Level Daemon Context
        List<(Guid Id, Guid TenantId)> pendingEvents;
        using (var discoveryScope = _scopeFactory.CreateScope())
        {
            var currentUser = discoveryScope.ServiceProvider.GetService<ICurrentUser>();
            var tenantContext = discoveryScope.ServiceProvider.GetService<ITenantContext>();
            var systemIdentity = SystemIdentityUtility.Current;

            // App-level daemon identity for global coordination
            currentUser?.SetSystemDaemon("OutboxMessageDispatcher", systemIdentity.FormatInternalDaemonEmail("OutboxMessageDispatcher"));
            tenantContext?.Initialize(
                tenantId: systemIdentity.RootTenantId,
                tenantName: systemIdentity.RootTenantName,
                userType: UserTypeEnum.CompanyUser,
                allowedTenants: [],
                tenantSlug: systemIdentity.RootTenantSlug,
                isRootTenant: true);

            var dbContext = discoveryScope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Recover events older than 15 seconds that are still marked as Published (not picked up by in-memory channel)
            var cutoffTime = DateTime.UtcNow.AddSeconds(-15);
            var batchSize = Math.Max(1, _options.Value.OutboxBatchSize);

            pendingEvents = await dbContext.EventPublishTrackers
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(e => e.Status == EventTrackerStatusEnum.Published && e.PublishedAtUtc <= cutoffTime && e.TriggerSource != "DomainEvent")
                .OrderBy(e => e.PublishedAtUtc)
                .Take(batchSize)
                .Select(e => new ValueTuple<Guid, Guid>(e.Id, e.TenantId))
                .ToListAsync(cancellationToken);
        }

        if (pendingEvents.Count == 0) return;

        var eventsByTenant = pendingEvents.GroupBy(p => p.Item2);

        _logger.LogInformation("Outbox Dispatcher found {Count} unprocessed published events across {TenantCount} tenant(s).",
            pendingEvents.Count, eventsByTenant.Count());

        // 2. Strict per-tenant execution: process each tenant group in its own isolated service scope
        foreach (var tenantGroup in eventsByTenant)
        {
            var tenantId = tenantGroup.Key;
            var targetIds = tenantGroup.Select(p => p.Item1).ToList();

            await ProcessTenantOutboxBatchAsync(tenantId, targetIds, cancellationToken);
        }
    }

    private async Task ProcessTenantOutboxBatchAsync(Guid tenantId, List<Guid> trackerIds, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();
        var currentUser = scope.ServiceProvider.GetService<ICurrentUser>();
        var tenantContext = scope.ServiceProvider.GetService<ITenantContext>();
        var systemIdentity = SystemIdentityUtility.Current;

        var isRoot = tenantId == systemIdentity.RootTenantId;
        var initialSlug = tenantId.ToString("N");

        // 1. Establish tenant context & bootstrap daemon identity BEFORE querying the database
        if (tenantContext != null)
        {
            tenantContext.Initialize(
                tenantId: tenantId,
                tenantName: "Tenant",
                userType: UserTypeEnum.CompanyUser,
                allowedTenants: [],
                tenantSlug: initialSlug,
                isRootTenant: isRoot);
        }

        if (currentUser != null)
        {
            var bootstrapDaemonEmail = systemIdentity.FormatInternalDaemonEmail($"OutboxDispatcher_{initialSlug}");
            currentUser.SetSystemDaemon($"OutboxDispatcher_{initialSlug}", bootstrapDaemonEmail);
        }

        // 2. Now queries on dbContext are fully authenticated and scoped to tenantId
        var tenantEntity = await dbContext.Tenants
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

        var tenantSlug = tenantEntity?.Slug ?? initialSlug;
        var tenantName = tenantEntity?.Name ?? "Tenant";

        if (tenantEntity != null && tenantContext != null)
        {
            tenantContext.Initialize(
                tenantId: tenantId,
                tenantName: tenantName,
                userType: UserTypeEnum.CompanyUser,
                allowedTenants: [],
                tenantSlug: tenantSlug,
                isRootTenant: isRoot);
        }

        // Look up dedicated tenant daemon user if one exists for this tenant
        var daemonMembership = await dbContext.TenantUsers
            .AsNoTracking()
            .Include(tu => tu.User)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(tu => tu.TenantId == tenantId && tu.User != null && tu.User.UserKind == UserKindEnum.SystemDaemon && !tu.IsDeleted, cancellationToken);

        if (currentUser != null)
        {
            if (daemonMembership?.User != null)
            {
                currentUser.RestoreUserContext(
                    daemonMembership.User.Id.ToString(),
                    daemonMembership.User.Email,
                    daemonMembership.User.FullName,
                    isRootAdmin: isRoot);
            }
            else
            {
                var daemonEmail = systemIdentity.FormatInternalDaemonEmail($"OutboxDispatcher_{tenantSlug}");
                currentUser.SetSystemDaemon($"OutboxDispatcher_{tenantSlug}", daemonEmail);
            }
        }

        // 3. Query the records for this tenant with tracking
        var records = await dbContext.EventPublishTrackers
            .AsTracking()
            .Where(e => trackerIds.Contains(e.Id) && e.TenantId == tenantId)
            .IgnoreQueryFilters()
            .ToListAsync(cancellationToken);

        if (records.Count == 0) return;

        await auditService.LogSecurityEventAsync(
            "OutboxRecoveryDispatched",
            AuditSeverityEnum.Information,
            $"Transactional Outbox recovered {records.Count} unprocessed event(s) for tenant '{tenantSlug}' ({tenantId}).",
            cancellationToken);

        foreach (var record in records)
        {
            try
            {
                var eventType = ResolveEventType(record.EventTypeFullName, record.EventName);
                if (eventType == null)
                {
                    _logger.LogWarning("Outbox event {EventId} ('{EventName}') type cannot be resolved for tenant {TenantId}. Marking as Failed.",
                        record.EventId, record.EventName, tenantId);
                    record.Status = EventTrackerStatusEnum.Failed;
                    continue;
                }

                var jobEvent = JsonSerializer.Deserialize(record.PayloadJson, eventType, JsonDefaults.StandardOptions) as IJobEvent;
                if (jobEvent == null)
                {
                    _logger.LogWarning("Outbox event {EventId} ('{EventName}') payload deserialization failed for tenant {TenantId}. Marking as Failed.",
                        record.EventId, record.EventName, tenantId);
                    record.Status = EventTrackerStatusEnum.Failed;
                    continue;
                }

                record.Status = EventTrackerStatusEnum.InProgress;
                await _queue.EnqueueAsync(jobEvent, cancellationToken);

                _logger.LogInformation("Outbox event {EventId} ('{EventName}') successfully recovered and dispatched for tenant {TenantId}.",
                    record.EventId, record.EventName, tenantId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to dispatch outbox event {EventId} ('{EventName}') for tenant {TenantId}.",
                    record.EventId, record.EventName, tenantId);
                record.Status = EventTrackerStatusEnum.Failed;
            }
        }

        dbContext.IsTransactionScopeCommitAuthorized = true;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            dbContext.IsTransactionScopeCommitAuthorized = false;
        }
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
