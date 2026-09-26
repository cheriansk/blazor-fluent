using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Events;
using BlazorFluent.Jobs.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Jobs.Listeners;

public class BatchJobQueueListener : BackgroundService
{
    private readonly IJobEventQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BatchJobQueueListener> _logger;

    public BatchJobQueueListener(
        IJobEventQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<BatchJobQueueListener> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Batch Job Queue Listener started. Waiting for job events...");

        try
        {
            await foreach (var jobEvent in _queue.ReadAllAsync(stoppingToken))
            {
                await ProcessJobEventAsync(jobEvent, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Batch Job Queue Listener is stopping due to host shutdown.");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Unhandled exception in Batch Job Queue Listener.");
        }
    }

    private async Task ProcessJobEventAsync(IJobEvent jobEvent, CancellationToken stoppingToken)
    {
        var eventType = jobEvent.GetType();
        _logger.LogInformation(
            "Processing batch job event {EventId} ({EventType}) triggered by {Source} for tenant {TenantId}",
            jobEvent.EventId, eventType.Name, jobEvent.TriggerSource, jobEvent.TenantId ?? "host");

        try
        {
            using var scope = _scopeFactory.CreateScope();

            // ─── RESTORE TENANT CONTEXT ───────────────────────────────────────────────
            // Background services run outside of an HTTP request scope. We must manually
            // restore the tenant context from the enqueued job event so that:
            //   • EF global query filters work correctly for tenant data isolation
            //   • Audit fields (CreatedBy, TenantId) are set to the originating tenant
            if (!string.IsNullOrWhiteSpace(jobEvent.TenantId))
            {
                var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
                tenantContext.Initialize(
                    tenantId: jobEvent.TenantId,
                    tenantName: null,               // Not needed for background processing
                    userType: UserType.CompanyUser, // Jobs are system-level; use CompanyUser to allow writes
                    allowedTenants: [],
                    isHost: false);
            }
            else
            {
                // Null TenantId → host-level job. The ITenantContext remains uninitialized
                // but EF context's IsHost = false by default; host jobs must call
                // IgnoreQueryFilters() explicitly if they need cross-tenant reads.
                _logger.LogDebug("Job event {EventId} has no TenantId — running as host-level job.", jobEvent.EventId);
            }
            // ─────────────────────────────────────────────────────────────────────────

            var handlerType = typeof(IBatchJobHandler<>).MakeGenericType(eventType);
            var handler = scope.ServiceProvider.GetService(handlerType);

            if (handler == null)
            {
                _logger.LogWarning("No IBatchJobHandler registered for event type {EventType}. Skipping event {EventId}.",
                    eventType.Name, jobEvent.EventId);
                return;
            }

            var method = handlerType.GetMethod(nameof(IBatchJobHandler<IJobEvent>.HandleAsync));
            if (method == null)
            {
                _logger.LogError("HandleAsync method not found on handler {HandlerType}.", handlerType.Name);
                return;
            }

            var task = (Task)method.Invoke(handler, [jobEvent, stoppingToken])!;
            await task;

            _logger.LogInformation("Successfully completed batch job event {EventId}.", jobEvent.EventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed executing batch job for event {EventId} ({EventType}).",
                jobEvent.EventId, eventType.Name);
        }
    }
}
