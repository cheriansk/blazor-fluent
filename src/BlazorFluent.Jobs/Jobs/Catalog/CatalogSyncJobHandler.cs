using BlazorFluent.Jobs.Abstractions;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Jobs.Jobs.Catalog;

/// <summary>
/// Sample batch job handler: synchronizes catalog items and verifies tenant isolation.
/// Auto-discovered and registered by <see cref="JobsExtensions"/>.
/// </summary>
public class CatalogSyncJobHandler : IBatchJobHandler<CatalogSyncJobEvent>
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<CatalogSyncJobHandler> _logger;

    public CatalogSyncJobHandler(AppDbContext dbContext, ILogger<CatalogSyncJobHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task HandleAsync(CatalogSyncJobEvent jobEvent, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing Catalog Sync batch job. TriggerSource={Source}, TenantId={TenantId}, BatchSize={BatchSize}",
            jobEvent.TriggerSource, jobEvent.TenantId ?? "Host", jobEvent.BatchSize);

        // Query products under the current restored tenant context
        var productCount = await _dbContext.Products
            .AsNoTracking()
            .CountAsync(cancellationToken);

        _logger.LogInformation("Catalog Sync processed successfully. Verified {Count} products for active tenant context.", productCount);

        // Simulate small work batch
        await Task.Delay(150, cancellationToken);
    }
}
