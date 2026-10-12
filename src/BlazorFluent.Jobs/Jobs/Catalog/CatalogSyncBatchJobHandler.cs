using BlazorFluent.Jobs.Abstractions;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Jobs.Jobs.Catalog;
/// <summary>
/// SAMPLE BATCH ONLY
/// </summary>
public class CatalogSyncBatchJobHandler : IBatchJobHandler<CatalogSyncJobEvent>
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<CatalogSyncBatchJobHandler> _logger;

    public CatalogSyncBatchJobHandler(AppDbContext dbContext, ILogger<CatalogSyncBatchJobHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task HandleAsync(CatalogSyncJobEvent jobEvent, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing Catalog Sync Batch Job. Triggered by: {TriggerSource}, BatchSize: {BatchSize}",
            jobEvent.TriggerSource, jobEvent.BatchSize);

        // Batch processing logic (e.g. sync inventory, adjust prices, or purge inactive items)
        try
        {
            var productCount = await _dbContext.Products.CountAsync(cancellationToken);
            _logger.LogInformation("Catalog Sync Batch: found {ProductCount} total products in database.", productCount);

            // Simulate batch work
            await Task.Delay(50, cancellationToken);

            _logger.LogInformation("Catalog Sync Batch successfully processed for Event {EventId}.", jobEvent.EventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during Catalog Sync Batch processing.");
            throw;
        }
    }
}
