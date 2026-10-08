using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.Domain.Jobs;
using BlazorFluent.Core.Dtos.Response;
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
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<JobManagerService> _logger;

    public JobManagerService(
        AppDbContext dbContext,
        IJobEventQueue queue,
        IAuditService auditService,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        ILogger<JobManagerService> logger)
    {
        _dbContext = dbContext;
        _queue = queue;
        _auditService = auditService;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<IReadOnlyList<JobExecutionEntity>> GetRecentExecutionsAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.JobExecutions.AsNoTracking();

        // Zero Trust: Non-host users can only see background jobs for their active tenant
        if (!_tenantContext.IsHost)
        {
            var activeTenant = _tenantContext.TenantId ?? string.Empty;
            query = query.Where(j => j.TenantId == activeTenant);
        }

        return await query
            .OrderByDescending(j => j.StartedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<Result> TriggerJobAsync(string jobName, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobName))
            return Result.Failure("JobName is required.");

        // Least Privilege: Non-host users can only trigger jobs for their own active tenant
        var targetTenantId = _tenantContext.IsHost
            ? tenantId
            : _tenantContext.TenantId;

        if (!_tenantContext.IsHost && string.IsNullOrWhiteSpace(targetTenantId))
        {
            return Result.Failure("Cannot trigger job: active tenant context is missing.");
        }

        _logger.LogInformation("Manually triggering job {JobName} for tenant {TenantId}", jobName, targetTenantId ?? "Host");

        var senderOrigin = $"Button:Run{jobName}";
        var senderUserId = _currentUser.UserId ?? "system";
        var senderUserEmail = _currentUser.Email ?? _currentUser.UserId ?? "system@daemon.local";
        var correlationId = Guid.CreateVersion7().ToString("N")[..12];

        // Map known job names to their respective typed event triggers
        if (jobName.Equals("CatalogSyncJob", StringComparison.OrdinalIgnoreCase) ||
            jobName.Equals("CatalogSyncJobEvent", StringComparison.OrdinalIgnoreCase))
        {
            var jobEvent = new CatalogSyncJobEvent(
                TriggerSource: senderOrigin,
                TenantId: targetTenantId,
                CorrelationId: correlationId,
                SenderOrigin: senderOrigin,
                SenderUserId: senderUserId,
                SenderUserEmail: senderUserEmail);

            await _queue.EnqueueAsync(jobEvent, cancellationToken);
        }
        else
        {
            // Generic catalog sync fallback for demo purposes
            var jobEvent = new CatalogSyncJobEvent(
                TriggerSource: senderOrigin,
                TenantId: targetTenantId,
                CorrelationId: correlationId,
                SenderOrigin: senderOrigin,
                SenderUserId: senderUserId,
                SenderUserEmail: senderUserEmail);

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

        // Zero Trust: Non-host users can only retry jobs for their active tenant
        if (!_tenantContext.IsHost && execution.TenantId != (_tenantContext.TenantId ?? string.Empty))
        {
            _logger.LogWarning("Security Violation: User '{UserId}' attempted to retry job execution {ExecutionId} in tenant '{JobTenant}' outside active tenant '{ActiveTenant}'",
                _currentUser.UserId, executionId, execution.TenantId, _tenantContext.TenantId);
            return Result.Failure("Access Denied: You cannot retry jobs belonging to another tenant.");
        }

        _logger.LogInformation("Initiating manual retry for job {JobName} (Previous Execution: {ExecutionId})",
            execution.JobName, executionId);

        var senderOrigin = $"Button:Retry{execution.JobName}";
        var retryUserId = _currentUser.UserId ?? "system";
        var retryUserEmail = _currentUser.Email ?? _currentUser.UserId ?? "system@daemon.local";
        var retryEvent = new CatalogSyncJobEvent(
            TriggerSource: senderOrigin,
            TenantId: execution.TenantId,
            CorrelationId: execution.CorrelationId,
            SenderOrigin: senderOrigin,
            SenderUserId: retryUserId,
            SenderUserEmail: retryUserEmail,
            ParentExecutionId: execution.Id);

        await _queue.EnqueueAsync(retryEvent, cancellationToken);

        await _auditService.LogUserActivityAsync(
            $"Initiated manual retry for background job '{execution.JobName}'",
            $"PreviousExecutionId: {executionId}, CorrelationId: {execution.CorrelationId}, TenantId: {execution.TenantId ?? "Host"}",
            cancellationToken);

        return Result.Success();
    }
}
