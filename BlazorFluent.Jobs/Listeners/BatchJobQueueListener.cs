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
        _logger.LogInformation("Processing batch job event {EventId} ({EventType}) triggered by {Source}",
            jobEvent.EventId, eventType.Name, jobEvent.TriggerSource);

        try
        {
            using var scope = _scopeFactory.CreateScope();
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
