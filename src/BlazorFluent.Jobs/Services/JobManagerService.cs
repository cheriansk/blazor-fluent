using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.Domain.Jobs;
using BlazorFluent.Core.Dtos.Response;
using BlazorFluent.Core.Events;
using BlazorFluent.Jobs.Abstractions;
using BlazorFluent.Jobs.Jobs.Audit;
using BlazorFluent.Jobs.Jobs.Tasks;
using BlazorFluent.Persistence.Context;
using BlazorFluent.Core.Utilities;
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

        // Zero Trust / Least Privilege: Enforce strict tenant isolation
        string targetTenantId;
        if (_tenantContext.IsHost)
        {
            // Host admin: use explicit tenantId if provided; otherwise fallback to active session;
            // if neither is available, fallback to the seeded root anchor SystemTenantSlug
            targetTenantId = !string.IsNullOrWhiteSpace(tenantId)
                ? tenantId
                : (!string.IsNullOrWhiteSpace(_tenantContext.TenantId)
                    ? _tenantContext.TenantId
                    : SystemIdentityUtility.SystemTenantSlug);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(_tenantContext.TenantId))
            {
                return Result.Failure("Cannot trigger job: active tenant context is missing.");
            }

            // Fail-closed: non-host users can NEVER trigger jobs for a foreign tenant
            if (!string.IsNullOrWhiteSpace(tenantId) && !string.Equals(tenantId, _tenantContext.TenantId, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Security Violation: User '{UserId}' in tenant '{ActiveTenant}' attempted to trigger job for foreign tenant '{TargetTenant}'",
                    _currentUser.UserId, _tenantContext.TenantId, tenantId);
                return Result.Failure("Access Denied: Non-host users cannot trigger background jobs for another tenant.");
            }

            targetTenantId = _tenantContext.TenantId;
        }

        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.Email))
        {
            return Result.Failure("Authentication and active user email are required to manually trigger background jobs.");
        }

        _logger.LogInformation("Manually triggering job {JobName} for tenant {TenantId}", jobName, targetTenantId);

        var senderOrigin = $"Button:Run{jobName}";
        var senderUserId = _currentUser.UserId ?? _currentUser.Email;
        var senderUserEmail = _currentUser.Email;
        var correlationId = Guid.CreateVersion7().ToString("N")[..12];

        // Map known job names to their respective typed event triggers
        BaseJobEvent? jobEvent = jobName switch
        {
            nameof(TaskCadenceAlertJobEvent) or "TaskCadenceAlertJob" => new TaskCadenceAlertJobEvent(
                TriggerSource: senderOrigin,
                TenantId: targetTenantId,
                CorrelationId: correlationId,
                SenderOrigin: senderOrigin,
                SenderUserId: senderUserId,
                SenderUserEmail: senderUserEmail),

            nameof(DailyTaskSummaryJobEvent) or "DailyTaskSummaryJob" => new DailyTaskSummaryJobEvent(
                TriggerSource: senderOrigin,
                TenantId: targetTenantId,
                CorrelationId: correlationId,
                SenderOrigin: senderOrigin,
                SenderUserId: senderUserId,
                SenderUserEmail: senderUserEmail),

            nameof(AuditPurgeJobEvent) or "AuditPurgeJob" => new AuditPurgeJobEvent(
                TriggerSource: senderOrigin,
                TenantId: targetTenantId,
                CorrelationId: correlationId,
                SenderOrigin: senderOrigin,
                SenderUserId: senderUserId,
                SenderUserEmail: senderUserEmail),

            _ => null
        };

        if (jobEvent is null)
        {
            _logger.LogWarning("Cannot trigger unknown job type: '{JobName}'", jobName);
            return Result.Failure($"Unknown job type: '{jobName}'. Supported jobs are: TaskCadenceAlertJob, DailyTaskSummaryJob, AuditPurgeJob.");
        }

        await _queue.EnqueueAsync(jobEvent, cancellationToken);

        await _auditService.LogUserActivityAsync(
            $"Triggered background job '{jobName}' manually",
            $"TenantId: {targetTenantId}",
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

        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.Email))
        {
            return Result.Failure("Authentication and active user email are required to retry background jobs.");
        }

        _logger.LogInformation("Initiating manual retry for job {JobName} (Previous Execution: {ExecutionId})",
            execution.JobName, executionId);

        var senderOrigin = $"Button:Retry{execution.JobName}";
        var retryUserId = _currentUser.UserId ?? _currentUser.Email;
        var retryUserEmail = _currentUser.Email;

        BaseJobEvent? retryEvent = execution.JobName switch
        {
            nameof(TaskCadenceAlertJobEvent) or "TaskCadenceAlertJob" => new TaskCadenceAlertJobEvent(
                TriggerSource: senderOrigin,
                TenantId: execution.TenantId,
                CorrelationId: execution.CorrelationId,
                SenderOrigin: senderOrigin,
                SenderUserId: retryUserId,
                SenderUserEmail: retryUserEmail,
                ParentExecutionId: execution.Id),

            nameof(DailyTaskSummaryJobEvent) or "DailyTaskSummaryJob" => new DailyTaskSummaryJobEvent(
                TriggerSource: senderOrigin,
                TenantId: execution.TenantId,
                CorrelationId: execution.CorrelationId,
                SenderOrigin: senderOrigin,
                SenderUserId: retryUserId,
                SenderUserEmail: retryUserEmail,
                ParentExecutionId: execution.Id),

            nameof(AuditPurgeJobEvent) or "AuditPurgeJob" => new AuditPurgeJobEvent(
                TriggerSource: senderOrigin,
                TenantId: execution.TenantId,
                CorrelationId: execution.CorrelationId,
                SenderOrigin: senderOrigin,
                SenderUserId: retryUserId,
                SenderUserEmail: retryUserEmail,
                ParentExecutionId: execution.Id),

            _ => null
        };

        if (retryEvent is null)
        {
            _logger.LogWarning("Cannot retry job execution {ExecutionId}: unsupported job name '{JobName}'", executionId, execution.JobName);
            return Result.Failure($"Cannot retry job: unsupported job name '{execution.JobName}'.");
        }

        await _queue.EnqueueAsync(retryEvent, cancellationToken);

        await _auditService.LogUserActivityAsync(
            $"Initiated manual retry for background job '{execution.JobName}'",
            $"PreviousExecutionId: {executionId}, CorrelationId: {execution.CorrelationId}, TenantId: {execution.TenantId ?? "Host"}",
            cancellationToken);

        return Result.Success();
    }
}
