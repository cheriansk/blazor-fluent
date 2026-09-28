using BlazorFluent.Jobs.Abstractions;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Jobs.Jobs.Audit;

/// <summary>
/// Batch job handler: purges audit.AuditRecords older than the configured retention window.
/// Runs cross-tenant (IgnoreQueryFilters) since maintenance jobs span all tenants.
/// Auto-discovered and registered by JobsExtensions.
/// </summary>
public class AuditPurgeJobHandler : IBatchJobHandler<AuditPurgeJobEvent>
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<AuditPurgeJobHandler> _logger;

    public AuditPurgeJobHandler(AppDbContext dbContext, ILogger<AuditPurgeJobHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task HandleAsync(AuditPurgeJobEvent jobEvent, CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddDays(-jobEvent.RetentionDays);

        _logger.LogInformation(
            "AuditPurgeJob starting. Purging audit records older than {Cutoff} (RetentionDays={Days})",
            cutoff, jobEvent.RetentionDays);

        var deleted = await _dbContext.AuditRecords
            .IgnoreQueryFilters()
            .Where(r => r.Created < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        _logger.LogInformation(
            "AuditPurgeJob completed. Deleted {Count} audit records older than {Cutoff}.",
            deleted, cutoff);
    }
}
