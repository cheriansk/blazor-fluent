using BlazorFluent.Core.Common;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.Domain.Jobs;
using BlazorFluent.Jobs.Abstractions;
using BlazorFluent.Jobs.Jobs.Catalog;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Jobs.Services;

/// <summary>
/// Service managing background job queries, on-demand execution triggers, and manual retries.
/// Fully audited via <see cref="IAuditService"/> and logged via Serilog.
/// </summary>
public class JobManagerService : IJobManagerService
{
    private readonly AppDbContext _dbContext;
    private readonly IJobEventQueue _queue;
    private readonly IAuditService _auditService;
    private readonly ILogger<JobManagerService> _logger;

    public JobManagerService(
        AppDbContext dbContext,
        IJobEventQueue queue,
        IAuditService auditService,
        ILogger<JobManagerService> logger)
    {
        _dbContext = dbContext;
        _queue = queue;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<JobExecutionEntity>> GetRecentExecutionsAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        return await _dbContext.JobExecutions
            .AsNoTracking()
            .OrderByDescending(j => j.StartedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<Result> TriggerJobAsync(string jobName, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobName))
            return Result.Failure("JobName is required.");

        _logger.LogInformation("Manually triggering job {JobName} for tenant {TenantId}", jobName, tenantId ?? "Host");

        // Map known job names to their respective typed event triggers
        if (jobName.Equals("CatalogSyncJob", StringComparison.OrdinalIgnoreCase) ||
            jobName.Equals("CatalogSyncJobEvent", StringComparison.OrdinalIgnoreCase))
        {
            var jobEvent = new CatalogSyncJobEvent(
                TriggerSource: "Manual",
                TenantId: tenantId);

            await _queue.EnqueueAsync(jobEvent, cancellationToken);
        }
        else
        {
            // Generic catalog sync fallback for demo purposes
            var jobEvent = new CatalogSyncJobEvent(
                TriggerSource: "Manual",
                TenantId: tenantId);

            await _queue.EnqueueAsync(jobEvent, cancellationToken);
        }

        await _auditService.LogUserActivityAsync(
            $"Triggered background job '{jobName}' manually",
            $"TenantId: {tenantId ?? "Host"}",
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result> RetryJobAsync(Guid executionId, CancellationToken cancellationToken = default)
    {
        var execution = await _dbContext.JobExecutions.FindAsync([executionId], cancellationToken);
        if (execution is null)
        {
            _logger.LogWarning("Manual retry failed: Job execution {ExecutionId} not found", executionId);
            return Result.Failure("Job execution not found.");
        }

        _logger.LogInformation("Initiating manual retry for job {JobName} (Previous Execution: {ExecutionId})",
            execution.JobName, executionId);

        var retryEvent = new CatalogSyncJobEvent(
            TriggerSource: "Manual Retry",
            TenantId: execution.TenantId);

        await _queue.EnqueueAsync(retryEvent, cancellationToken);

        await _auditService.LogUserActivityAsync(
            $"Initiated manual retry for background job '{execution.JobName}'",
            $"PreviousExecutionId: {executionId}, TenantId: {execution.TenantId ?? "Host"}",
            cancellationToken);

        return Result.Success();
    }
}
