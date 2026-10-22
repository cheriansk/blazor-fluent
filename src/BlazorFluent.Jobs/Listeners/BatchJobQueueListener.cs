using System.Diagnostics;
using BlazorFluent.Core.Configuration;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Core.Domain.Jobs;
using BlazorFluent.Core.Events;
using BlazorFluent.Jobs.Abstractions;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlazorFluent.Jobs.Listeners;

/// <summary>
/// Background worker processing queued batch job events with:
/// - 3-attempt retries with exponential backoff
/// - Execution history recorded in PostgreSQL (JobExecutionEntity)
/// - Forensic auditing via IAuditService
/// - Multi-tenant context preservation
/// - Graceful draining on host shutdown
/// </summary>
public class BatchJobQueueListener : BackgroundService
{
    private const int MaxRetries = 3;
    private readonly IJobEventQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IOptions<JobsOptions> _jobsOptions;
    private readonly ILogger<BatchJobQueueListener> _logger;

    public BatchJobQueueListener(
        IJobEventQueue queue,
        IServiceScopeFactory scopeFactory,
        IHostApplicationLifetime lifetime,
        IOptions<JobsOptions> jobsOptions,
        ILogger<BatchJobQueueListener> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _lifetime = lifetime;
        _jobsOptions = jobsOptions;
        _logger = logger;

        // Graceful channel completion on host shutdown
        _lifetime.ApplicationStopping.Register(() => _queue.Complete());
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
        if (jobEvent.TenantId == Guid.Empty)
        {
            _logger.LogCritical("Zero-Trust Security Violation: Batch job event {EventId} missing required TenantId. Aborting job execution (Fail-Closed).", jobEvent.EventId);
            return;
        }

        var tenantId = jobEvent.TenantId;
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["TenantId"] = tenantId,
            ["JobId"] = jobEvent.EventId,
            ["CorrelationId"] = jobEvent.CorrelationId
        }))
        {
            var eventType = jobEvent.GetType();
            var jobName = eventType.Name.Replace("Event", string.Empty);

            _logger.LogInformation(
                "Processing batch job {JobName} ({EventId}, CorrelationId={CorrelationId}) triggered by {Source} for tenant {TenantId}",
                jobName, jobEvent.EventId, jobEvent.CorrelationId, jobEvent.TriggerSource, tenantId);

            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var auditService = scope.ServiceProvider.GetService<IAuditService>();
            var eventTracker = scope.ServiceProvider.GetService<IEventTrackerService>();

            // ─── 1. RESTORE TENANT CONTEXT (LEAST PRIVILEGE) ─────────────────────────────
            var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();

            tenantContext.Initialize(
                tenantId: tenantId,
                tenantName: tenantId.ToString(),
                userType: UserTypeEnum.CompanyUser,
                allowedTenants: [],
                isHost: tenantId == BlazorFluent.Core.Utilities.SystemIdentityUtility.SystemDaemonTenantId);

            var currentUser = scope.ServiceProvider.GetService<ICurrentUser>();
            var isCron = string.Equals(jobEvent.TriggerSource, "Cron", StringComparison.OrdinalIgnoreCase);

            // Lookup tenant-scoped system daemon account
            var daemonUser = await dbContext.TenantUsers
                .AsNoTracking()
                .Where(tu => tu.TenantId == tenantId && tu.User.UserKind == UserKindEnum.SystemDaemon && !tu.IsDeleted)
                .Select(tu => tu.User)
                .FirstOrDefaultAsync(stoppingToken);

            if (isCron)
            {
                // Dedicated Per-Tenant Automated Background Processing Kill Switch Check
                if (daemonUser != null && !daemonUser.IsActive)
                {
                    _logger.LogInformation(
                        "Tenant '{TenantId}' daemon account ('{DaemonEmail}') is suspended. Gracefully skipping automated batch job '{JobName}'.",
                        tenantId, daemonUser.Email, jobName);

                    if (auditService != null)
                    {
                        await auditService.LogSecurityEventAsync(
                            "BatchJobSkippedDaemonSuspended",
                            AuditSeverityEnum.Information,
                            $"Automated batch job '{jobName}' skipped because tenant '{tenantId}' daemon account is suspended.",
                            stoppingToken);
                    }
                    return;
                }

                if (currentUser != null)
                {
                    if (daemonUser != null)
                    {
                        currentUser.RestoreUserContext(daemonUser.Id.ToString(), daemonUser.Email, daemonUser.FullName, isRootAdmin: false);
                        currentUser.SetSystemDaemon($"BatchJob:{jobName}", daemonUser.Email);
                    }
                    else
                    {
                        var cronEmail = !string.IsNullOrWhiteSpace(jobEvent.SenderUserEmail) 
                            ? jobEvent.SenderUserEmail 
                            : _jobsOptions.Value.CronServiceAccountEmail;

                        if (string.IsNullOrWhiteSpace(cronEmail))
                        {
                            throw new InvalidOperationException($"Zero-Trust Security Violation: Job '{jobName}' has no sender email and Jobs:CronServiceAccountEmail is not configured.");
                        }

                        currentUser.SetSystemDaemon($"BatchJob:{jobName}", cronEmail);
                    }
                }
            }
            else
            {
                // User-triggered event: Restore initiating user context
                if (currentUser != null)
                {
                    var userEmail = jobEvent.SenderUserEmail;
                    if (string.IsNullOrWhiteSpace(userEmail))
                    {
                        throw new InvalidOperationException($"Zero-Trust Security Violation: Interactive job '{jobName}' must specify a valid sender identity.");
                    }

                    var userId = !string.IsNullOrWhiteSpace(jobEvent.SenderUserId) 
                        ? jobEvent.SenderUserId 
                        : Guid.Empty.ToString();
                    var userName = !string.IsNullOrWhiteSpace(jobEvent.SenderOrigin) 
                        ? jobEvent.SenderOrigin 
                        : userEmail;
                    currentUser.RestoreUserContext(userId, userEmail, userName);
                }
            }

            // ─── 2. RECORD INITIAL EXECUTION STAMP ────────────────────────────────────────
            var execution = new JobExecutionEntity
            {
                Id = Guid.CreateVersion7(),
                JobName = jobName,
                TenantId = jobEvent.TenantId ,
                TriggerSource = JobTriggerSourceExtensions.ParseTriggerSource(jobEvent.TriggerSource),
                CorrelationId = !string.IsNullOrWhiteSpace(jobEvent.CorrelationId) ? jobEvent.CorrelationId : Guid.CreateVersion7().ToString("N")[..12],
                ParentExecutionId = jobEvent.ParentExecutionId,
                Status = JobStatusEnum.Running,
                AttemptCount = 1,
                MaxRetries = MaxRetries,
                StartedAt = DateTime.UtcNow
            };

            dbContext.JobExecutions.Add(execution);
            await dbContext.SaveChangesAsync(stoppingToken);

            // ─── 3. RESOLVE HANDLER ──────────────────────────────────────────────────────
            var handlerType = typeof(IBatchJobHandler<>).MakeGenericType(eventType);
            var handler = scope.ServiceProvider.GetService(handlerType);

            if (handler == null)
            {
                _logger.LogWarning("No IBatchJobHandler registered for event type {EventType}. Marking job {EventId} as failed.",
                    eventType.Name, jobEvent.EventId);

                execution.Status = JobStatusEnum.Failed;
                execution.CompletedAt = DateTime.UtcNow;
                execution.ErrorMessage = $"No IBatchJobHandler registered for {eventType.Name}.";
                await dbContext.SaveChangesAsync(stoppingToken);

                if (eventTracker != null)
                {
                    var failId = await eventTracker.TrackConsumptionStartAsync(
                        jobEvent.EventId,
                        "UnknownHandler",
                        "HandleAsync",
                        cancellationToken: stoppingToken);

                    await eventTracker.TrackConsumptionCompleteAsync(
                        failId,
                        isSuccess: false,
                        durationMs: 0,
                        errorMessage: $"No IBatchJobHandler registered for {eventType.Name}.",
                        cancellationToken: stoppingToken);
                }
                return;
            }

            var method = handlerType.GetMethod(nameof(IBatchJobHandler<IJobEvent>.HandleAsync));
            if (method == null)
            {
                _logger.LogError("HandleAsync method not found on handler {HandlerType}.", handlerType.Name);
                execution.Status = JobStatusEnum.Failed;
                execution.CompletedAt = DateTime.UtcNow;
                execution.ErrorMessage = $"HandleAsync not found on {handlerType.Name}.";
                await dbContext.SaveChangesAsync(stoppingToken);
                return;
            }

            // ─── 4. EXECUTE WITH 3-ATTEMPT RETRY LOOP & EXPONENTIAL BACKOFF ──────────────
            var stopwatch = Stopwatch.StartNew();
            Exception? lastException = null;

            Guid consumptionId = Guid.Empty;
            if (eventTracker != null)
            {
                consumptionId = await eventTracker.TrackConsumptionStartAsync(
                    jobEvent.EventId,
                    handler.GetType().Name,
                    method.Name,
                    attemptCount: 1,
                    cancellationToken: stoppingToken);
            }

            for (var attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    if (attempt > 1)
                    {
                        execution.AttemptCount = attempt;
                        execution.Status = JobStatusEnum.Retrying;
                        await dbContext.SaveChangesAsync(stoppingToken);
                    }

                    var task = (Task)method.Invoke(handler, [jobEvent, stoppingToken])!;
                    await task;

                    // Succeeded!
                    stopwatch.Stop();
                    execution.Status = JobStatusEnum.Succeeded;
                    execution.CompletedAt = DateTime.UtcNow;
                    execution.DurationMs = stopwatch.Elapsed.TotalMilliseconds;
                    execution.ErrorMessage = null;
                    await dbContext.SaveChangesAsync(stoppingToken);

                    if (eventTracker != null && consumptionId != Guid.Empty)
                    {
                        await eventTracker.TrackConsumptionCompleteAsync(
                            consumptionId,
                            isSuccess: true,
                            durationMs: stopwatch.Elapsed.TotalMilliseconds,
                            cancellationToken: stoppingToken);
                    }

                    if (auditService != null)
                    {
                        await auditService.LogUserActivityAsync(
                            $"Background batch job '{jobName}' succeeded",
                            $"JobId: {jobEvent.EventId}, CorrelationId: {jobEvent.CorrelationId}, Attempts: {attempt}/{MaxRetries}, Duration: {stopwatch.ElapsedMilliseconds}ms",
                            stoppingToken);
                    }

                    _logger.LogInformation(
                        "Successfully completed batch job {JobName} ({EventId}, CorrelationId={CorrelationId}) on attempt {Attempt}/{MaxRetries} in {Duration}ms.",
                        jobName, jobEvent.EventId, jobEvent.CorrelationId, attempt, MaxRetries, stopwatch.ElapsedMilliseconds);
                    return;
                }
                catch (Exception ex) when (attempt < MaxRetries && !stoppingToken.IsCancellationRequested)
                {
                    lastException = ex;
                    var backoffDelay = TimeSpan.FromMilliseconds(500 * Math.Pow(2, attempt - 1));

                    _logger.LogWarning(ex,
                        "Batch job {JobName} ({EventId}, CorrelationId={CorrelationId}) failed attempt {Attempt}/{MaxRetries}. Retrying in {Delay}ms...",
                        jobName, jobEvent.EventId, jobEvent.CorrelationId, attempt, MaxRetries, backoffDelay.TotalMilliseconds);

                    execution.AttemptCount = attempt;
                    execution.Status = JobStatusEnum.Retrying;
                    execution.ErrorMessage = $"Attempt {attempt}/{MaxRetries} failed: {ex.Message}";
                    await dbContext.SaveChangesAsync(stoppingToken);

                    await Task.Delay(backoffDelay, stoppingToken);
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    break;
                }
            }

            // All attempts exhausted: Flag as Failed
            stopwatch.Stop();
            execution.Status = JobStatusEnum.Failed;
            execution.CompletedAt = DateTime.UtcNow;
            execution.DurationMs = stopwatch.Elapsed.TotalMilliseconds;
            execution.ErrorMessage = lastException?.ToString() ?? "Unknown batch job failure.";
            await dbContext.SaveChangesAsync(stoppingToken);

            if (eventTracker != null && consumptionId != Guid.Empty)
            {
                await eventTracker.TrackConsumptionCompleteAsync(
                    consumptionId,
                    isSuccess: false,
                    durationMs: stopwatch.Elapsed.TotalMilliseconds,
                    errorMessage: lastException?.Message,
                    exceptionDetails: lastException?.ToString(),
                    cancellationToken: stoppingToken);
            }

            if (auditService != null)
            {
                await auditService.LogSecurityEventAsync(
                    $"Background batch job '{jobName}' failed after {MaxRetries} attempts",
                    AuditSeverityEnum.Error,
                    $"JobId: {jobEvent.EventId}, CorrelationId: {jobEvent.CorrelationId}, Error: {lastException?.Message}",
                    stoppingToken);
            }

            _logger.LogError(lastException,
                "Batch job {JobName} ({EventId}) permanently failed after {MaxRetries} attempts.",
                jobName, jobEvent.EventId, MaxRetries);
        }
    }
}
