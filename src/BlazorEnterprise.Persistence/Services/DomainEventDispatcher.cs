using System.Security;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Events;
using BlazorEnterprise.Core.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BlazorEnterprise.Persistence.Services;

/// <summary>
/// Domain Event Dispatcher implementing Milan Jovanović's domain events pattern.
/// Resolves and invokes all registered IDomainEventHandler<TDomainEvent> implementations
/// and records publish and consumption tracking into IEventTrackerService via an isolated scope.
/// </summary>
public class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<DomainEventDispatcher> _logger;

    public DomainEventDispatcher(
        IServiceProvider serviceProvider,
        IServiceScopeFactory scopeFactory,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        ILogger<DomainEventDispatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _scopeFactory = scopeFactory;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        var eventType = domainEvent.GetType();

        // Enforce strict Zero-Trust tenant validation: an event must NEVER be dispatched without a valid TenantId.
        var resolvedTenantId = ResolveEventTenantId(domainEvent);
        if (resolvedTenantId == Guid.Empty)
        {
            _logger.LogError("Zero-Trust Security Violation: Domain event {EventName} ({EventId}) cannot be dispatched because TenantId is empty or uninitialized.",
                eventType.Name, domainEvent.EventId);
            throw new SecurityException($"Zero-Trust Security Violation: Domain event '{eventType.Name}' cannot be dispatched because TenantId is empty or uninitialized.");
        }

        // Safely extract sender identity without throwing Zero-Trust unauthenticated circuit exceptions
        var resolvedUserId = ExtractPropertyString(domainEvent, "UserId") ?? (_currentUser.IsAuthenticated ? _currentUser.UserId : null);
        var resolvedUserEmail = ExtractPropertyString(domainEvent, "UserEmail") ?? (_currentUser.IsAuthenticated ? _currentUser.Email : null);

        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(eventType);
        var handlers = _serviceProvider.GetServices(handlerType).ToList();

        // Resolve correlation ID: domainEvent property -> ambient transaction scope -> fallback to EventId
        var resolvedCorrelationId = !string.IsNullOrWhiteSpace(domainEvent.CorrelationId)
            ? domainEvent.CorrelationId
            : (AmbientTransactionContext.Current?.ScopeId.ToString("N")[..12] ?? domainEvent.EventId.ToString("N")[..12]);

        // Record publish in Event Tracker using an isolated scope
        Guid eventTrackerId = Guid.Empty;
        try
        {
            using var isolatedScope = _scopeFactory.CreateScope();
            PopulateScopeContext(isolatedScope, resolvedTenantId);
            var eventTracker = isolatedScope.ServiceProvider.GetService<IEventTrackerService>();
            if (eventTracker != null)
            {
                eventTrackerId = await eventTracker.TrackPublishAsync(
                    domainEvent,
                    domainEvent.EventId,
                    correlationId: resolvedCorrelationId,
                    triggerSource: "DomainEvent",
                    sourceClass: nameof(DomainEventDispatcher),
                    sourceMethod: nameof(DispatchAsync),
                    senderUserId: resolvedUserId,
                    senderUserEmail: resolvedUserEmail,
                    cancellationToken: cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record event publish tracking for domain event {EventName}", eventType.Name);
        }

        _logger.LogInformation("Dispatching Domain Event {EventName} ({EventId}) to {HandlerCount} handler(s).",
            eventType.Name, domainEvent.EventId, handlers.Count);

        foreach (var handler in handlers)
        {
            if (handler == null) continue;
            var handlerName = handler.GetType().Name;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Guid consumptionTrackerId = Guid.Empty;

            try
            {
                using var isolatedScope = _scopeFactory.CreateScope();
                PopulateScopeContext(isolatedScope, resolvedTenantId);
                var eventTracker = isolatedScope.ServiceProvider.GetService<IEventTrackerService>();
                if (eventTracker != null && eventTrackerId != Guid.Empty)
                {
                    consumptionTrackerId = await eventTracker.TrackConsumptionStartAsync(
                        domainEvent.EventId,
                        handlerName,
                        "HandleAsync",
                        cancellationToken: cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to record consumption start for handler {HandlerName}", handlerName);
            }

            try
            {
                var method = handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync));
                if (method != null)
                {
                    var task = (Task)method.Invoke(handler, [domainEvent, cancellationToken])!;
                    await task;
                }

                sw.Stop();
                _logger.LogInformation("Handler {HandlerName} completed domain event {EventName} in {ElapsedMs:F2}ms",
                    handlerName, eventType.Name, sw.Elapsed.TotalMilliseconds);

                if (consumptionTrackerId != Guid.Empty)
                {
                    try
                    {
                        using var isolatedScope = _scopeFactory.CreateScope();
                        PopulateScopeContext(isolatedScope, resolvedTenantId);
                        var eventTracker = isolatedScope.ServiceProvider.GetService<IEventTrackerService>();
                        if (eventTracker != null)
                        {
                            await eventTracker.TrackConsumptionCompleteAsync(
                                consumptionTrackerId,
                                isSuccess: true,
                                durationMs: sw.Elapsed.TotalMilliseconds,
                                cancellationToken: cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to record consumption completion for handler {HandlerName}", handlerName);
                    }
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "Handler {HandlerName} failed processing domain event {EventName} ({EventId})",
                    handlerName, eventType.Name, domainEvent.EventId);

                if (consumptionTrackerId != Guid.Empty)
                {
                    try
                    {
                        using var isolatedScope = _scopeFactory.CreateScope();
                        PopulateScopeContext(isolatedScope, resolvedTenantId);
                        var eventTracker = isolatedScope.ServiceProvider.GetService<IEventTrackerService>();
                        if (eventTracker != null)
                        {
                            await eventTracker.TrackConsumptionCompleteAsync(
                                consumptionTrackerId,
                                isSuccess: false,
                                durationMs: sw.Elapsed.TotalMilliseconds,
                                errorMessage: ex.Message,
                                exceptionDetails: ex.ToString(),
                                cancellationToken: cancellationToken);
                        }
                    }
                    catch (Exception trackerEx)
                    {
                        _logger.LogWarning(trackerEx, "Failed to record consumption error for handler {HandlerName}", handlerName);
                    }
                }
            }
        }
    }

    public async Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvents);
        foreach (var domainEvent in domainEvents)
        {
            await DispatchAsync(domainEvent, cancellationToken);
        }
    }

    private Guid ResolveEventTenantId(IDomainEvent domainEvent)
    {
        if (_tenantContext.HasActiveTenant && _tenantContext.TenantId != Guid.Empty)
        {
            return _tenantContext.TenantId;
        }

        var prop = domainEvent.GetType().GetProperty("TenantId");
        if (prop != null && prop.PropertyType == typeof(Guid))
        {
            var eventTenantId = (Guid?)prop.GetValue(domainEvent);
            if (eventTenantId.HasValue && eventTenantId.Value != Guid.Empty)
            {
                return eventTenantId.Value;
            }
        }

        return Guid.Empty;
    }

    private static string? ExtractPropertyString(IDomainEvent domainEvent, string propertyName)
    {
        var prop = domainEvent.GetType().GetProperty(propertyName);
        if (prop != null && prop.PropertyType == typeof(string))
        {
            return (string?)prop.GetValue(domainEvent);
        }
        return null;
    }

    private void PopulateScopeContext(IServiceScope scope, Guid tenantId)
    {
        var scopeTenant = scope.ServiceProvider.GetService<ITenantContext>();
        if (scopeTenant != null && !scopeTenant.HasActiveTenant)
        {
            if (_tenantContext.HasActiveTenant && _tenantContext.TenantId != Guid.Empty)
            {
                scopeTenant.SetTenant(_tenantContext.TenantId, _tenantContext.TenantSlug, _tenantContext.TenantName);
            }
            else if (tenantId != Guid.Empty)
            {
                scopeTenant.SetTenant(tenantId, "domain-event", "Domain Event Scope");
            }
        }

        var scopeUser = scope.ServiceProvider.GetService<ICurrentUser>();
        if (scopeUser != null && !scopeUser.IsAuthenticated)
        {
            if (_currentUser.IsAuthenticated)
            {
                scopeUser.RestoreUserContext(_currentUser.UserId, _currentUser.Email, _currentUser.UserName, _currentUser.IsRootAdmin);
            }
            else
            {
                var systemIdentity = SystemIdentityUtility.Current;
                var daemonEmail = systemIdentity.FormatInternalDaemonEmail("DomainEventDispatcher");
                scopeUser.SetSystemDaemon("DomainEventDispatcher", daemonEmail);
            }
        }
    }
}
