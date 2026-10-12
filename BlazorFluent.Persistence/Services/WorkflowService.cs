using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Tasks;
using BlazorFluent.Core.Dtos;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Persistence.Services;

/// <summary>
/// Domain service implementing the enterprise hierarchical sequential workflow engine.
/// Enforces Zero Trust caller authorization, a constant Workflow Owner, steps as TaskEntity instances,
/// and child SubTaskEntity items delegated to individual team members across Tenant, Program, and Project scopes.
/// </summary>
public class WorkflowService : IWorkflowService
{
    private readonly AppDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IHierarchyAuthorizationService _hierarchyAuth;
    private readonly INotifyService _notifyService;
    private readonly ILogger<WorkflowService> _logger;

    public WorkflowService(
        AppDbContext dbContext,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IHierarchyAuthorizationService hierarchyAuth,
        INotifyService notifyService,
        ILogger<WorkflowService> logger)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _hierarchyAuth = hierarchyAuth;
        _notifyService = notifyService;
        _logger = logger;
    }

    public async Task<WorkflowTaskEntity> CreateWorkflowAsync(CreateWorkflowReqDto request, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId ?? throw new InvalidOperationException("Active tenant context is required.");
        if (request.Steps == null || request.Steps.Count == 0)
        {
            throw new ArgumentException("A workflow requires at least one sequential step.", nameof(request));
        }

        if (request.Level == TaskLevel.Program && (!request.ProgramId.HasValue || request.ProgramId == Guid.Empty))
        {
            throw new ArgumentException("A valid Program ID is required for Program-scoped workflows.", nameof(request));
        }

        if (request.Level == TaskLevel.Project && (!request.ProjectId.HasValue || request.ProjectId == Guid.Empty))
        {
            throw new ArgumentException("A valid Project ID is required for Project-scoped workflows.", nameof(request));
        }

        var callerEmail = _currentUser.Email ?? "system@company.local";
        var workflowOwner = !string.IsNullOrWhiteSpace(request.WorkflowOwnerEmail)
            ? request.WorkflowOwnerEmail.Trim()
            : callerEmail;

        var workflow = new WorkflowTaskEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Level = request.Level,
            ProgramId = request.Level == TaskLevel.Program ? request.ProgramId : null,
            ProjectId = request.Level == TaskLevel.Project ? request.ProjectId : null,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Priority = request.Priority,
            Status = TaskStatus.InProgress,
            StartDate = DateTime.UtcNow,
            DueDate = request.DueDate ?? DateTime.UtcNow.AddDays(14),
            ReporterEmail = callerEmail,
            WatcherEmails = request.WatcherEmails?.Trim() ?? string.Empty,
            Labels = "Workflow",
            WorkflowOwnerEmail = workflowOwner
        };

        var order = 1;
        foreach (var stepDto in request.Steps)
        {
            var isFirst = order == 1;
            var stepTask = new TaskEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Level = request.Level,
                ProgramId = workflow.ProgramId,
                ProjectId = (Guid)workflow.ProjectId,
                WorkflowTaskId = workflow.Id,
                WorkflowStepOrder = order,
                WorkflowStepStatus = isFirst ? WorkflowStepStatus.Active : WorkflowStepStatus.Pending,
                Title = stepDto.Title.Trim(),
                Description = stepDto.Description?.Trim() ?? string.Empty,
                Priority = request.Priority,
                Status = isFirst ? TaskStatus.InProgress : TaskStatus.Open,
                StartDate = DateTime.UtcNow,
                DueDate = stepDto.DueDate ?? DateTime.UtcNow.AddDays(3),
                ReporterEmail = callerEmail,
                AssigneeEmails = stepDto.StepOwnerEmail.Trim(),
                Labels = "WorkflowStep",
                WorkflowStepActivatedAtUtc = isFirst ? DateTime.UtcNow : null
            };

            // Process any initial subtasks delegated to team members
            if (stepDto.SubTasks != null && stepDto.SubTasks.Count > 0)
            {
                var subOrder = 1;
                foreach (var subDto in stepDto.SubTasks)
                {
                    if (string.IsNullOrWhiteSpace(subDto.Title)) continue;

                    var subTask = new SubTaskEntity
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        ParentTaskId = stepTask.Id,
                        OrderIndex = subOrder++,
                        Title = subDto.Title.Trim(),
                        AssigneeEmails = subDto.AssigneeEmail?.Trim() ?? string.Empty,
                        DueDate = subDto.DueDate ?? stepTask.DueDate,
                        StartDate = DateTime.UtcNow,
                        Status = TaskStatus.Open,
                        Priority = request.Priority,
                        ReporterEmail = callerEmail,
                        Level = request.Level,
                        ProgramId = workflow.ProgramId,
                        ProjectId = workflow.ProjectId
                    };
                    stepTask.SubTasks.Add(subTask);
                }
            }

            workflow.Steps.Add(stepTask);
            if (isFirst)
            {
                workflow.CurrentStepId = stepTask.Id;
            }
            order++;
        }

        _dbContext.WorkflowTasks.Add(workflow);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Launched workflow '{Title}' ({Id}) at Level {Level}. Constant Owner: {Owner}. Steps: {StepCount}",
            workflow.Title, workflow.Id, workflow.Level, workflow.WorkflowOwnerEmail, workflow.Steps.Count);

        // Notify initial step owner
        var firstStep = workflow.Steps.First();
        await DispatchNotificationSilentlyAsync(
            recipientEmail: firstStep.AssigneeEmails,
            title: $"Workflow Action Required: {workflow.Title}",
            message: $"You are the assigned Step Owner of Step 1: '{firstStep.Title}'. Target deadline: {firstStep.DueDate:yyyy-MM-dd}.",
            severity: NotificationSeverity.Warning,
            category: NotificationCategory.Personal,
            projectId: workflow.ProjectId,
            ct: ct);

        // If workflow owner is distinct from step owner, send confirmation alert
        if (!string.Equals(workflow.WorkflowOwnerEmail, firstStep.AssigneeEmails, StringComparison.OrdinalIgnoreCase))
        {
            await DispatchNotificationSilentlyAsync(
                recipientEmail: workflow.WorkflowOwnerEmail,
                title: $"Workflow Launched: {workflow.Title}",
                message: $"Workflow launched successfully with {workflow.Steps.Count} steps. Step 1 is active under {firstStep.AssigneeEmails}.",
                severity: NotificationSeverity.Info,
                category: NotificationCategory.Personal,
                projectId: workflow.ProjectId,
                ct: ct);
        }

        return workflow;
    }

    public async Task<WorkflowTaskEntity> CompleteStepAsync(CompleteWorkflowStepReqDto request, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId ?? throw new InvalidOperationException("Active tenant context is required.");
        var callerEmail = _currentUser.Email ?? string.Empty;

        var workflow = await _dbContext.WorkflowTasks
            .Include(w => w.Steps)
                .ThenInclude(s => s.SubTasks)
            .FirstOrDefaultAsync(w => w.Id == request.WorkflowId && w.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException($"Workflow '{request.WorkflowId}' not found in active tenant.");

        var activeStep = workflow.Steps.FirstOrDefault(s => s.Id == request.StepId)
            ?? throw new InvalidOperationException($"Step Task '{request.StepId}' not found in workflow.");

        // Zero Trust Gate 1: Step must be currently active
        if (activeStep.WorkflowStepStatus != WorkflowStepStatus.Active || workflow.CurrentStepId != activeStep.Id)
        {
            throw new InvalidOperationException($"Zero Trust Violation: Step '{activeStep.Title}' is not in Active status (Current status: {activeStep.WorkflowStepStatus}).");
        }

        var isStepOwner = string.Equals(callerEmail, activeStep.AssigneeEmails, StringComparison.OrdinalIgnoreCase) ||
                          activeStep.GetParsedAssigneeEmails().Contains(callerEmail, StringComparer.OrdinalIgnoreCase);
        var isWorkflowOwner = string.Equals(callerEmail, workflow.WorkflowOwnerEmail, StringComparison.OrdinalIgnoreCase);
        var isTenantAdmin = await _hierarchyAuth.IsTenantAdminAsync(workflow.TenantId, _currentUser.UserId ?? string.Empty, ct);
        var isAdmin = _currentUser.IsRootAdmin || isTenantAdmin;

        if (!isStepOwner && !isWorkflowOwner && !isAdmin)
        {
            _logger.LogWarning("SECURITY ALERT: Caller '{Caller}' attempted unauthorized completion of workflow step '{StepId}' assigned to '{Owner}'",
                callerEmail, activeStep.Id, activeStep.AssigneeEmails);
            throw new UnauthorizedAccessException($"Zero Trust Violation: You are not authorized to complete step '{activeStep.Title}'. Assigned owner: {activeStep.AssigneeEmails}.");
        }

        // Auto-close any remaining open subtasks under this step
        foreach (var sub in activeStep.SubTasks.Where(s => !s.IsClosed))
        {
            sub.Status = TaskStatus.Closed;
            sub.EndDate = DateTime.UtcNow;
        }

        // Advance Step
        activeStep.WorkflowStepStatus = WorkflowStepStatus.Completed;
        activeStep.Status = TaskStatus.Closed;
        activeStep.WorkflowStepCompletedAtUtc = DateTime.UtcNow;
        activeStep.EndDate = DateTime.UtcNow;
        activeStep.WorkflowStepCompletionNotes = request.CompletionNotes?.Trim();

        var priorStepOwner = activeStep.AssigneeEmails;
        var nextStep = workflow.Steps
            .OrderBy(s => s.WorkflowStepOrder)
            .FirstOrDefault(s => s.WorkflowStepOrder == activeStep.WorkflowStepOrder + 1);

        if (nextStep != null)
        {
            // Sequential Handoff to Next Step
            nextStep.WorkflowStepStatus = WorkflowStepStatus.Active;
            nextStep.Status = TaskStatus.InProgress;
            nextStep.WorkflowStepActivatedAtUtc = DateTime.UtcNow;

            workflow.CurrentStepId = nextStep.Id;
            // Constant WorkflowOwnerEmail remains untouched!

            _logger.LogInformation("Advanced workflow '{Title}' ({Id}) from Step {PriorOrder} ({PriorOwner}) to Step {NextOrder} ({NextOwner})",
                workflow.Title, workflow.Id, activeStep.WorkflowStepOrder, priorStepOwner, nextStep.WorkflowStepOrder, nextStep.AssigneeEmails);

            await _dbContext.SaveChangesAsync(ct);

            // Notify next step owner
            await DispatchNotificationSilentlyAsync(
                recipientEmail: nextStep.AssigneeEmails,
                title: $"Workflow Action Required: {workflow.Title}",
                message: $"Step {activeStep.WorkflowStepOrder} ('{activeStep.Title}') was signed off by {callerEmail}. You are now the active Step Owner of Step {nextStep.WorkflowStepOrder}: '{nextStep.Title}'.",
                severity: NotificationSeverity.Warning,
                category: NotificationCategory.Personal,
                projectId: workflow.ProjectId,
                ct: ct);

            // Notify constant workflow owner
            if (!string.Equals(workflow.WorkflowOwnerEmail, nextStep.AssigneeEmails, StringComparison.OrdinalIgnoreCase))
            {
                await DispatchNotificationSilentlyAsync(
                    recipientEmail: workflow.WorkflowOwnerEmail,
                    title: $"Workflow Progress: {workflow.Title}",
                    message: $"Step {activeStep.WorkflowStepOrder} ('{activeStep.Title}') completed. Step {nextStep.WorkflowStepOrder} ('{nextStep.Title}') is now active under {nextStep.AssigneeEmails}.",
                    severity: NotificationSeverity.Info,
                    category: NotificationCategory.Personal,
                    projectId: workflow.ProjectId,
                    ct: ct);
            }

            // Notify watchers and prior step owner
            var watchers = ParseEmails(workflow.WatcherEmails).Append(priorStepOwner).Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var watcher in watchers)
            {
                if (!string.Equals(watcher, nextStep.AssigneeEmails, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(watcher, workflow.WorkflowOwnerEmail, StringComparison.OrdinalIgnoreCase))
                {
                    await DispatchNotificationSilentlyAsync(
                        recipientEmail: watcher,
                        title: $"Workflow Progress: {workflow.Title}",
                        message: $"Step {activeStep.WorkflowStepOrder} completed. Handoff to {nextStep.AssigneeEmails} for Step {nextStep.WorkflowStepOrder} ('{nextStep.Title}').",
                        severity: NotificationSeverity.Info,
                        category: NotificationCategory.Generic,
                        projectId: workflow.ProjectId,
                        ct: ct);
                }
            }
        }
        else
        {
            // All sequential steps finished -> Complete workflow
            workflow.Status = TaskStatus.Closed;
            workflow.EndDate = DateTime.UtcNow;
            workflow.CurrentStepId = null;

            _logger.LogInformation("Completed workflow '{Title}' ({Id}) successfully. All steps delivered.", workflow.Title, workflow.Id);
            await _dbContext.SaveChangesAsync(ct);

            var watchers = ParseEmails(workflow.WatcherEmails)
                .Concat(workflow.Steps.Select(s => s.AssigneeEmails))
                .Append(workflow.WorkflowOwnerEmail)
                .Append(workflow.ReporterEmail)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var watcher in watchers)
            {
                await DispatchNotificationSilentlyAsync(
                    recipientEmail: watcher,
                    title: $"Workflow Completed: {workflow.Title}",
                    message: $"All sequential steps for workflow '{workflow.Title}' have been delivered and closed successfully.",
                    severity: NotificationSeverity.Success,
                    category: NotificationCategory.Generic,
                    projectId: workflow.ProjectId,
                    ct: ct);
            }
        }

        return workflow;
    }

    public async Task<WorkflowTaskEntity> ReassignStepAsync(ReassignWorkflowStepReqDto request, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId ?? throw new InvalidOperationException("Active tenant context is required.");
        var callerEmail = _currentUser.Email ?? string.Empty;

        var workflow = await _dbContext.WorkflowTasks
            .Include(w => w.Steps)
                .ThenInclude(s => s.SubTasks)
            .FirstOrDefaultAsync(w => w.Id == request.WorkflowId && w.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException($"Workflow '{request.WorkflowId}' not found.");

        var targetStep = workflow.Steps.FirstOrDefault(s => s.Id == request.StepId)
            ?? throw new InvalidOperationException($"Step '{request.StepId}' not found.");

        var isStepOwner = string.Equals(callerEmail, targetStep.AssigneeEmails, StringComparison.OrdinalIgnoreCase) ||
                          targetStep.GetParsedAssigneeEmails().Contains(callerEmail, StringComparer.OrdinalIgnoreCase);
        var isWorkflowOwner = string.Equals(callerEmail, workflow.WorkflowOwnerEmail, StringComparison.OrdinalIgnoreCase);
        var isTenantAdmin = await _hierarchyAuth.IsTenantAdminAsync(workflow.TenantId, _currentUser.UserId ?? string.Empty, ct);
        var isAdmin = _currentUser.IsRootAdmin || isTenantAdmin;

        if (!isStepOwner && !isWorkflowOwner && !isAdmin)
        {
            throw new UnauthorizedAccessException("Zero Trust Violation: Only the Step Owner, Workflow Owner, or an Admin can reassign a workflow step.");
        }

        var priorOwner = targetStep.AssigneeEmails;
        var newOwner = request.NewOwnerEmail.Trim();

        targetStep.AssigneeEmails = newOwner;

        var auditNote = $"[Reassigned from {priorOwner} to {newOwner} by {callerEmail} on {DateTime.UtcNow:u}: {request.Reason}]";
        targetStep.WorkflowStepCompletionNotes = string.IsNullOrEmpty(targetStep.WorkflowStepCompletionNotes)
            ? auditNote
            : $"{targetStep.WorkflowStepCompletionNotes}\n{auditNote}";

        await _dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("Reassigned workflow step '{StepTitle}' ({StepId}) to '{NewOwner}'. Reason: {Reason}",
            targetStep.Title, targetStep.Id, newOwner, request.Reason);

        // Notify new step owner
        await DispatchNotificationSilentlyAsync(
            recipientEmail: newOwner,
            title: $"Workflow Step Assigned: {workflow.Title}",
            message: $"You were assigned Step {targetStep.WorkflowStepOrder} ('{targetStep.Title}') by {callerEmail}. Reason: {request.Reason}",
            severity: NotificationSeverity.Warning,
            category: NotificationCategory.Personal,
            projectId: workflow.ProjectId,
            ct: ct);

        // Notify constant workflow owner if distinct
        if (!string.Equals(workflow.WorkflowOwnerEmail, newOwner, StringComparison.OrdinalIgnoreCase))
        {
            await DispatchNotificationSilentlyAsync(
                recipientEmail: workflow.WorkflowOwnerEmail,
                title: $"Step Reassigned: {workflow.Title}",
                message: $"Step {targetStep.WorkflowStepOrder} was reassigned from {priorOwner} to {newOwner}. Reason: {request.Reason}",
                severity: NotificationSeverity.Info,
                category: NotificationCategory.Personal,
                projectId: workflow.ProjectId,
                ct: ct);
        }

        return workflow;
    }

    public async Task<SubTaskEntity> AddSubTaskToStepAsync(AddSubTaskToStepReqDto request, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId ?? throw new InvalidOperationException("Active tenant context is required.");
        var callerEmail = _currentUser.Email ?? string.Empty;

        var workflow = await _dbContext.WorkflowTasks
            .Include(w => w.Steps)
                .ThenInclude(s => s.SubTasks)
            .FirstOrDefaultAsync(w => w.Id == request.WorkflowId && w.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException($"Workflow '{request.WorkflowId}' not found.");

        var stepTask = workflow.Steps.FirstOrDefault(s => s.Id == request.StepTaskId)
            ?? throw new InvalidOperationException($"Step Task '{request.StepTaskId}' not found in workflow.");

        var isStepOwner = string.Equals(callerEmail, stepTask.AssigneeEmails, StringComparison.OrdinalIgnoreCase) ||
                          stepTask.GetParsedAssigneeEmails().Contains(callerEmail, StringComparer.OrdinalIgnoreCase);
        var isWorkflowOwner = string.Equals(callerEmail, workflow.WorkflowOwnerEmail, StringComparison.OrdinalIgnoreCase);
        var isTenantAdmin = await _hierarchyAuth.IsTenantAdminAsync(workflow.TenantId, _currentUser.UserId ?? string.Empty, ct);
        var isAdmin = _currentUser.IsRootAdmin || isTenantAdmin;

        if (!isStepOwner && !isWorkflowOwner && !isAdmin)
        {
            throw new UnauthorizedAccessException("Zero Trust Violation: Only the Step Owner, Workflow Owner, or an Admin can delegate subtasks.");
        }

        var subTask = new SubTaskEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ParentTaskId = stepTask.Id,
            OrderIndex = stepTask.SubTasks.Count + 1,
            Title = request.Title.Trim(),
            AssigneeEmails = request.AssigneeEmail.Trim(),
            DueDate = request.DueDate ?? stepTask.DueDate,
            StartDate = DateTime.UtcNow,
            Status = TaskStatus.Open,
            Priority = stepTask.Priority,
            ReporterEmail = callerEmail,
            Level = stepTask.Level,
            ProgramId = stepTask.ProgramId,
            ProjectId = stepTask.ProjectId
        };

        _dbContext.SubTasks.Add(subTask);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Added subtask '{Title}' under workflow step '{StepTitle}' delegated to '{Assignee}'",
            subTask.Title, stepTask.Title, subTask.AssigneeEmails);

        // Notify assigned team member
        if (!string.IsNullOrWhiteSpace(subTask.AssigneeEmails))
        {
            await DispatchNotificationSilentlyAsync(
                recipientEmail: subTask.AssigneeEmails,
                title: $"New Subtask Delegated: {subTask.Title}",
                message: $"You were assigned a subtask under Step '{stepTask.Title}' of workflow '{workflow.Title}'. Due: {subTask.DueDate:yyyy-MM-dd}.",
                severity: NotificationSeverity.Info,
                category: NotificationCategory.Personal,
                projectId: workflow.ProjectId,
                ct: ct);
        }

        return subTask;
    }

    public async Task<SubTaskEntity> ToggleSubTaskStatusAsync(ToggleSubTaskStatusReqDto request, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId ?? throw new InvalidOperationException("Active tenant context is required.");

        var subTask = await _dbContext.SubTasks
            .FirstOrDefaultAsync(s => s.Id == request.SubTaskId && s.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException($"SubTask '{request.SubTaskId}' not found.");

        subTask.Status = request.IsClosed ? TaskStatus.Closed : TaskStatus.Open;
        subTask.EndDate = request.IsClosed ? DateTime.UtcNow : null;

        await _dbContext.SaveChangesAsync(ct);
        return subTask;
    }

    public async Task<IReadOnlyList<WorkflowTaskEntity>> GetWorkflowsAsync(WorkflowFilterDto filter, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId ?? throw new InvalidOperationException("Active tenant context is required.");

        var query = _dbContext.WorkflowTasks
            .Include(w => w.Steps)
                .ThenInclude(s => s.SubTasks)
            .Where(w => w.TenantId == tenantId && !w.IsDeleted);

        if (filter.Level.HasValue)
        {
            query = query.Where(w => w.Level == filter.Level.Value);
        }

        if (filter.ProgramId.HasValue && filter.ProgramId != Guid.Empty)
        {
            query = query.Where(w => w.ProgramId == filter.ProgramId.Value);
        }

        if (filter.ProjectId.HasValue && filter.ProjectId != Guid.Empty)
        {
            query = query.Where(w => w.ProjectId == filter.ProjectId.Value);
        }

        if (filter.IsCompleted.HasValue)
        {
            query = filter.IsCompleted.Value
                ? query.Where(w => w.Status == TaskStatus.Closed)
                : query.Where(w => w.Status != TaskStatus.Closed);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim().ToLowerInvariant();
            query = query.Where(w => w.Title.ToLower().Contains(term) ||
                                     w.WorkflowOwnerEmail.ToLower().Contains(term) ||
                                     w.Description.ToLower().Contains(term));
        }

        return await query
            .OrderByDescending(w => w.Created)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<WorkflowTaskEntity>> GetProjectWorkflowsAsync(Guid projectId, CancellationToken ct = default)
    {
        return await GetWorkflowsAsync(new WorkflowFilterDto(Level: TaskLevel.Project, ProjectId: projectId), ct);
    }

    public async Task<WorkflowTaskEntity?> GetWorkflowByIdAsync(Guid workflowId, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId ?? throw new InvalidOperationException("Active tenant context is required.");

        return await _dbContext.WorkflowTasks
            .Include(w => w.Steps)
                .ThenInclude(s => s.SubTasks)
            .FirstOrDefaultAsync(w => w.Id == workflowId && w.TenantId == tenantId && !w.IsDeleted, ct);
    }

    private async Task DispatchNotificationSilentlyAsync(
        string recipientEmail,
        string title,
        string message,
        NotificationSeverity severity,
        NotificationCategory category,
        Guid? projectId,
        CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(recipientEmail)) return;

            var norm = recipientEmail.Trim().ToLowerInvariant();
            var user = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email.ToLower() == norm, ct);
            var userId = user != null ? user.Id.ToString() : recipientEmail;

            var req = new SendNotificationReqDto
            {
                Title = title,
                Message = message,
                Category = category,
                Severity = severity,
                UserId = userId,
                ProjectId = projectId ?? Guid.Empty,
                LinkUrl = "workflows",
                Channels = NotificationChannels.InApp | NotificationChannels.Email
            };

            await _notifyService.SendAsync(req, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dispatch workflow notification to {Recipient}: {Title}", recipientEmail, title);
        }
    }

    private static IEnumerable<string> ParseEmails(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) yield break;
        var parts = raw.Split(new[] { ';', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var p in parts)
        {
            if (!string.IsNullOrWhiteSpace(p)) yield return p;
        }
    }
}
