using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Shared.DataListTypes;
using BlazorEnterprise.Jobs.Abstractions;
using BlazorEnterprise.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using BlazorEnterprise.Persistence.Extensions;
using BlazorEnterprise.Shared.Extensions;

namespace BlazorEnterprise.Jobs.Jobs.Audit;

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
        var now = DateTime.UtcNow;
        _logger.LogInformation("AuditPurgeJob starting multi-tier retention purge at {Now:yyyy-MM-dd HH:mm:ss} UTC.", now);

        var totalDeleted = 0;
        var retentionTiers = DataListExtensions.GetDataListItems<AuditRetentionPeriodEnum>();

        foreach (var tierItem in retentionTiers)
        {
            var tier = tierItem.Value;
            var cutoff = now.AddDays(-(int)tier);

            var deleted = await _dbContext.AuditRecords
                .Where(r => r.RetentionDays == tier && r.Created < cutoff)
                .ExecuteDeleteWithAuditedBypassAsync(
                    _auditService,
                    $"Cross-tenant AuditPurgeJob execution: purging {tier} ({((int)tier)} days) records older than {cutoff:yyyy-MM-dd}.",
                    cancellationToken);

            if (deleted > 0)
            {
                _logger.LogInformation("Purged {Count} audit records under {Tier} retention tier (cutoff: {Cutoff:yyyy-MM-dd}).",
                    deleted, tier, cutoff);
                totalDeleted += deleted;
            }
        }

        _logger.LogInformation(
            "AuditPurgeJob completed. Purged {Count} total expired audit records across all retention tiers.",
            totalDeleted);
    }
}
