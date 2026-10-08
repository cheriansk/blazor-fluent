using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Events;
using BlazorFluent.Core.Dtos.Requests;
using BlazorFluent.Core.Dtos.Response;
using BlazorFluent.Core.Events;
using BlazorFluent.Core.Utilities;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace BlazorFluent.Persistence.Services;

public class EventTrackerService : IEventTrackerService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly AppDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<EventTrackerService> _logger;

    public EventTrackerService(
        AppDbContext dbContext,
        ICurrentUser currentUser,
        ITenantContext tenantContext,
        ILogger<EventTrackerService> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<Guid> TrackPublishAsync<TEvent>(
        TEvent @event,
        Guid? eventId = null,
        string? correlationId = null,
        string? triggerSource = null,
        string? sourceClass = null,
        [CallerMemberName] string sourceMethod = "",
        [CallerFilePath] string sourceFilePath = "",
        string? senderOrigin = null,
        string? senderUserId = null,
        string? senderUserEmail = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Use runtime type so concrete derived event types (e.g. CatalogSyncJobEvent) are captured accurately
            var runtimeType = @event?.GetType() ?? typeof(TEvent);
            var eventName = runtimeType.Name;
            var eventTypeFullName = runtimeType.FullName;
            var actualEventId = eventId ?? Guid.CreateVersion7();

            var isSystemOrCron = string.Equals(triggerSource, "Cron", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(triggerSource, "System", StringComparison.OrdinalIgnoreCase);

            string tenantId;
            if (isSystemOrCron)
            {
                // System/Cron batch triggers default to active tenant context or default anchor tenant
                tenantId = !string.IsNullOrWhiteSpace(_tenantContext.TenantId) 
                    ? _tenantContext.TenantId 
                    : IRootAdminService.DefaultTenantSlug;
            }
            else
            {
                // User-triggered events: Tenant is strictly mandatory unless caller is Host
                if (!_tenantContext.IsHost && string.IsNullOrWhiteSpace(_tenantContext.TenantId))
                {
                    _logger.LogError("Mandatory tenant validation failed: User-triggered event '{EventType}' cannot be dispatched without an active tenant.", eventName);
                    throw new InvalidOperationException(
                        $"Mandatory tenant validation failed: User-triggered event '{eventName}' cannot be dispatched " +
                        $"because TenantId is null or empty. Ensure the active tenant is properly initialized before dispatching events.");
                }

                tenantId = _tenantContext.IsHost
                    ? (!string.IsNullOrWhiteSpace(_tenantContext.TenantId) ? _tenantContext.TenantId : IRootAdminService.DefaultTenantSlug)
                    : _tenantContext.TenantId!;
            }

            // Infer class name from source file path if not passed explicitly
            var resolvedSourceClass = !string.IsNullOrWhiteSpace(sourceClass)
                ? sourceClass
                : (!string.IsNullOrWhiteSpace(sourceFilePath)
                    ? Path.GetFileNameWithoutExtension(sourceFilePath)
                    : "UnknownPublisher");

            // Serialize event payload using runtime type so derived properties are captured
            string payloadJson;
            try
            {
                payloadJson = JsonSerializer.Serialize(@event, runtimeType, JsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to serialize event payload of type {EventType}", eventName);
                payloadJson = $"{{\"serializationError\": \"{ex.Message}\"}}";
            }

            var jobEvent = @event as IJobEvent;
            var resolvedSenderOrigin = senderOrigin ?? jobEvent?.SenderOrigin ?? triggerSource;
            var resolvedUserId = senderUserId ?? jobEvent?.SenderUserId ?? _currentUser.UserId;
            var resolvedUserEmail = senderUserEmail ?? jobEvent?.SenderUserEmail ?? _currentUser.Email;

            if (string.IsNullOrWhiteSpace(resolvedUserId) && isSystemOrCron)
            {
                var daemonName = $"cron:{eventName.Replace("Event", string.Empty)}";
                resolvedUserId = daemonName;
                if (string.IsNullOrWhiteSpace(resolvedUserEmail))
                {
                    resolvedUserEmail = "cron-daemon@blazorfluent.local";
                }
            }
            else if (string.IsNullOrWhiteSpace(resolvedUserId) && string.IsNullOrWhiteSpace(resolvedUserEmail))
            {
                resolvedUserId = SystemIdentityUtility.ResolveAuditableUserId(_currentUser, $"PublishEvent:{eventName}");
                resolvedUserEmail = _currentUser.Email;
            }

            if (string.IsNullOrWhiteSpace(resolvedUserId))
            {
                throw new InvalidOperationException($"Zero-Trust Security Violation: Cannot track event publish for '{eventName}'. Sender identity is missing.");
            }

            var record = new EventPublishTrackerEntity
            {
                Id = Guid.CreateVersion7(),
                EventId = actualEventId,
                TenantId = tenantId,
                CorrelationId = !string.IsNullOrWhiteSpace(correlationId) ? correlationId : actualEventId.ToString("N")[..12],
                EventName = eventName,
                EventTypeFullName = eventTypeFullName,
                SourceClass = resolvedSourceClass,
                SourceMethod = sourceMethod,
                SourceFilePath = sourceFilePath,
                UserId = resolvedUserId,
                UserEmail = resolvedUserEmail ?? resolvedUserId,
                TriggerSource = resolvedSenderOrigin ?? triggerSource ?? (isSystemOrCron ? "System" : "Manual"),
                PayloadJson = payloadJson,
                PublishedAtUtc = DateTime.UtcNow,
                Status = EventTrackerStatus.Published,
                ConsumerCount = 0
            };

            _dbContext.EventPublishTrackers.Add(record);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "EventTracker [Published]: Event {EventName} ({EventId}, CorrelationId={CorrelationId}) published by {SourceClass}.{SourceMethod} (User: {UserId})",
                record.EventName, record.EventId, record.CorrelationId, record.SourceClass, record.SourceMethod, record.UserId);

            return record.Id;
        }
        catch (InvalidOperationException)
        {
            // Fail-fast security exception: do not swallow mandatory tenant violations
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record event publish tracking ledger for event type {EventType}", typeof(TEvent).Name);
            return Guid.Empty;
        }
    }

    public async Task<Guid> TrackConsumptionStartAsync(
        Guid eventId,
        string consumerClass,
        string consumerMethod,
        int attemptCount = 1,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Lookup parent publication tracker by EventId or primary key Id with AsTracking
            var parent = await _dbContext.EventPublishTrackers
                .AsTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(e => e.EventId == eventId || e.Id == eventId, cancellationToken);

            var tenantId = parent?.TenantId ?? (!string.IsNullOrWhiteSpace(_tenantContext.TenantId) ? _tenantContext.TenantId : IRootAdminService.DefaultTenantSlug);
            var correlationId = parent?.CorrelationId ?? eventId.ToString("N")[..12];

            var consumption = new EventConsumptionTrackerEntity
            {
                Id = Guid.CreateVersion7(),
                EventPublishTrackerId = parent?.Id ?? Guid.Empty,
                TenantId = tenantId,
                CorrelationId = correlationId,
                ConsumerClass = consumerClass,
                ConsumerMethod = consumerMethod,
                StartedAtUtc = DateTime.UtcNow,
                Status = EventConsumptionStatus.Running,
                AttemptCount = attemptCount
            };

            _dbContext.EventConsumptionTrackers.Add(consumption);

            if (parent != null)
            {
                if (parent.Status == EventTrackerStatus.Published)
                {
                    parent.Status = EventTrackerStatus.InProgress;
                }
                _dbContext.EventPublishTrackers.Update(parent);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "EventTracker [Consumption Started]: Consumer {ConsumerClass}.{ConsumerMethod} started processing Event {EventId} (Attempt {Attempt})",
                consumerClass, consumerMethod, eventId, attemptCount);

            return consumption.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record consumption start for event {EventId}", eventId);
            return Guid.Empty;
        }
    }

    public async Task TrackConsumptionCompleteAsync(
        Guid consumptionTrackerId,
        bool isSuccess,
        double durationMs,
        string? errorMessage = null,
        string? exceptionDetails = null,
        CancellationToken cancellationToken = default)
    {
        if (consumptionTrackerId == Guid.Empty) return;

        try
        {
            var consumption = await _dbContext.EventConsumptionTrackers
                .AsTracking()
                .IgnoreQueryFilters()
                .Include(c => c.PublishTracker)
                .FirstOrDefaultAsync(c => c.Id == consumptionTrackerId, cancellationToken);

            if (consumption is null)
            {
                _logger.LogWarning("EventTracker: Consumption record {ConsumptionId} not found.", consumptionTrackerId);
                return;
            }

            consumption.CompletedAtUtc = DateTime.UtcNow;
            consumption.DurationMs = durationMs;
            consumption.Status = isSuccess ? EventConsumptionStatus.Succeeded : EventConsumptionStatus.Failed;
            consumption.ErrorMessage = errorMessage;
            consumption.ExceptionDetails = exceptionDetails;

            _dbContext.EventConsumptionTrackers.Update(consumption);

            // Recalculate parent aggregate status based on all its consumptions
            if (consumption.PublishTracker != null)
            {
                var siblingConsumptions = await _dbContext.EventConsumptionTrackers
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Where(c => c.EventPublishTrackerId == consumption.EventPublishTrackerId)
                    .ToListAsync(cancellationToken);

                var total = siblingConsumptions.Count;
                var succeededCount = siblingConsumptions.Count(c => c.Id == consumption.Id ? isSuccess : c.Status == EventConsumptionStatus.Succeeded);
                var failedCount = siblingConsumptions.Count(c => c.Id == consumption.Id ? !isSuccess : c.Status == EventConsumptionStatus.Failed);

                consumption.PublishTracker.ConsumerCount = total;

                if (failedCount > 0 && succeededCount > 0)
                {
                    consumption.PublishTracker.Status = EventTrackerStatus.PartiallyFailed;
                }
                else if (failedCount > 0)
                {
                    consumption.PublishTracker.Status = EventTrackerStatus.Failed;
                }
                else if (succeededCount == total)
                {
                    consumption.PublishTracker.Status = EventTrackerStatus.Completed;
                }

                _dbContext.EventPublishTrackers.Update(consumption.PublishTracker);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "EventTracker [Consumption Completed]: Consumer {ConsumerClass}.{ConsumerMethod} finished in {Duration}ms. Status: {Status}",
                consumption.ConsumerClass, consumption.ConsumerMethod, durationMs, consumption.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record consumption completion for record {ConsumptionId}", consumptionTrackerId);
        }
    }

    public async Task<PagedResultRespDto<EventPublishTrackerEntity>> GetEventsAsync(
        EventTrackerFilterReqDto filter,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.EventPublishTrackers
            .IgnoreQueryFilters()
            .Include(e => e.Consumptions)
            .AsSplitQuery()
            .AsNoTracking();

        // Enforce tenant boundary unless user is host
        if (!_tenantContext.IsHost)
        {
            var tenantId = _tenantContext.TenantId ?? string.Empty;
            query = query.Where(e => e.TenantId == tenantId);
        }
        else if (!string.IsNullOrWhiteSpace(filter.TenantId))
        {
            if (string.Equals(filter.TenantId, IRootAdminService.DefaultTenantSlug, StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(e => e.TenantId == IRootAdminService.DefaultTenantSlug);
            }
            else
            {
                query = query.Where(e => e.TenantId == filter.TenantId);
            }
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(e => e.Status == filter.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.EventName))
        {
            var name = filter.EventName.Trim();
            query = query.Where(e => e.EventName.Contains(name));
        }

        if (!string.IsNullOrWhiteSpace(filter.SourceClass))
        {
            var sc = filter.SourceClass.Trim();
            query = query.Where(e => e.SourceClass.Contains(sc));
        }

        if (!string.IsNullOrWhiteSpace(filter.UserId))
        {
            query = query.Where(e => e.UserId == filter.UserId);
        }

        if (filter.FromDate.HasValue)
        {
            query = query.Where(e => e.PublishedAtUtc >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(e => e.PublishedAtUtc <= filter.ToDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim().ToLower();
            query = query.Where(e =>
                e.EventName.ToLower().Contains(term) ||
                e.CorrelationId.ToLower().Contains(term) ||
                e.SourceClass.ToLower().Contains(term) ||
                e.SourceMethod.ToLower().Contains(term) ||
                (e.UserEmail != null && e.UserEmail.ToLower().Contains(term)) ||
                (e.PayloadJson != null && e.PayloadJson.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(e => e.PublishedAtUtc)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return PagedResultRespDto<EventPublishTrackerEntity>.Create(items, totalCount, filter.PageNumber, filter.PageSize);
    }

    public async Task<IReadOnlyList<EventConsumptionTrackerEntity>> GetConsumptionsAsync(
        Guid publishTrackerId,
        CancellationToken cancellationToken = default)
    {
        var publishTracker = await _dbContext.EventPublishTrackers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == publishTrackerId, cancellationToken);

        if (publishTracker is null) return [];

        if (!_tenantContext.IsHost && publishTracker.TenantId != (_tenantContext.TenantId ?? string.Empty))
        {
            _logger.LogWarning("Security Violation: User '{UserId}' attempted to view consumption logs for event '{EventId}' in tenant '{EventTenant}' outside active tenant '{ActiveTenant}'.",
                _currentUser.UserId, publishTrackerId, publishTracker.TenantId, _tenantContext.TenantId);
            return [];
        }

        return await _dbContext.EventConsumptionTrackers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(c => c.EventPublishTrackerId == publishTrackerId)
            .OrderBy(c => c.StartedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<EventPublishTrackerEntity?> GetEventByIdAsync(
        Guid eventTrackerId,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.EventPublishTrackers
            .IgnoreQueryFilters()
            .Include(e => e.Consumptions)
            .AsSplitQuery()
            .AsNoTracking();

        if (!_tenantContext.IsHost)
        {
            var activeTenant = _tenantContext.TenantId ?? string.Empty;
            query = query.Where(e => e.TenantId == activeTenant);
        }

        return await query.FirstOrDefaultAsync(e => e.Id == eventTrackerId || e.EventId == eventTrackerId, cancellationToken);
    }
}
