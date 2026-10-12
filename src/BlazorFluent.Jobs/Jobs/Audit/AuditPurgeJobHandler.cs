using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Jobs.Abstractions;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Jobs.Jobs.Audit;

/// <summary>
/// Batch job handler: purges audit.AuditRecords older than the configured retention window.
/// Runs cross-tenant (IgnoreQueryFilters) since maintenance jobs span all tenants, with audited justification.
/// Auto-discovered and registered by JobsExtensions.
/// </summary>
public class AuditPurgeJobHandler : IBatchJobHandler<AuditPurgeJobEvent>
{
    private readonly AppDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly ILogger<AuditPurgeJobHandler> _logger;

    public AuditPurgeJobHandler(
        AppDbContext dbContext,
        IAuditService auditService,
        ILogger<AuditPurgeJobHandler> logger)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task HandleAsync(AuditPurgeJobEvent jobEvent, CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddDays(-jobEvent.RetentionDays);

        _logger.LogInformation(
            "AuditPurgeJob starting. Purging audit records older than {Cutoff} (RetentionDays={Days})",
            cutoff, jobEvent.RetentionDays);

        await _auditService.LogSecurityEventAsync(
            "QueryFilterBypass",
            AuditSeverity.Warning,
            $"Cross-tenant AuditPurgeJob batch execution: purging records older than {cutoff:yyyy-MM-dd} (RetentionDays={jobEvent.RetentionDays}).",
            cancellationToken);

        var deleted = await _dbContext.AuditRecords
            .IgnoreQueryFilters()
            .Where(r => r.Created < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        _logger.LogInformation(
            "AuditPurgeJob completed. Deleted {Count} audit records older than {Cutoff}.",
            deleted, cutoff);
    }
}
