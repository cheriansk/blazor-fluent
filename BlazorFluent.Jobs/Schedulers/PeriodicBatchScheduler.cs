using BlazorFluent.Jobs.Abstractions;
using BlazorFluent.Jobs.Jobs.Catalog;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Jobs.Schedulers;

public class PeriodicBatchScheduler : BackgroundService
{
    private readonly IJobEventQueue _queue;
    private readonly ILogger<PeriodicBatchScheduler> _logger;
    private readonly TimeSpan _period = TimeSpan.FromMinutes(10); // Default interval

    public PeriodicBatchScheduler(IJobEventQueue queue, ILogger<PeriodicBatchScheduler> logger)
    {
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Periodic Batch Scheduler started with interval: {Period}", _period);

        using var timer = new PeriodicTimer(_period);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                _logger.LogInformation("Scheduler interval elapsed. Dispatching CatalogSyncJobEvent to queue...");

                var jobEvent = new CatalogSyncJobEvent(TriggerSource: "Scheduler:PeriodicTimer");
                await _queue.EnqueueAsync(jobEvent, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Periodic Batch Scheduler stopping.");
        }
    }
}
