using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Core.Domain.Tasks;
using BlazorFluent.Core.Dtos;
using BlazorFluent.Core.Utilities;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TaskStatus = BlazorFluent.Shared.DataListTypes.TaskStatus;

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

    public async Task<WorkflowEntity> CreateWorkflowAsync(CreateWorkflowReqDto request, CancellationToken ct = default)
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

        var callerEmail = !string.IsNullOrWhiteSpace(_currentUser.Email) 
            ? _currentUser.Email 
            : SystemIdentityUtility.ResolveAuditableUserId(_currentUser, "CreateWorkflow");

        // Zero Trust Gate: Admin permissions required to create/design workflows per level
        var canManage = await CanUserManageWorkflowAsync(request.Level, request.ProgramId, request.ProjectId, ct);
        if (!canManage)
        {
            _logger.LogWarning("SECURITY ALERT: User '{User}' denied permission to create {Level} workflow.", callerEmail, request.Level);
            throw new UnauthorizedAccessException($"Zero Trust Violation: Only designated administrators can create workflows at the {request.Level} level.");
        }

        var workflowOwner = !string.IsNullOrWhiteSpace(request.WorkflowOwnerEmail)
            ? request.WorkflowOwnerEmail.Trim()
            : callerEmail;

        var workflow = new WorkflowEntity
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
            TaskEntity stepTask;

            if (stepDto.ExistingTaskId.HasValue && stepDto.ExistingTaskId.Value != Guid.Empty)
            {
                var existing = await _dbContext.Tasks
                    .Include(t => t.SubTasks)
                    .FirstOrDefaultAsync(t => t.Id == stepDto.ExistingTaskId.Value && t.TenantId == tenantId, ct)
                    ?? throw new InvalidOperationException($"Existing task '{stepDto.ExistingTaskId.Value}' not found.");

                if (existing.WorkflowId.HasValue && existing.WorkflowId != Guid.Empty && existing.WorkflowId != workflow.Id)
                {
                    throw new InvalidOperationException($"Task '{existing.Title}' already belongs to another workflow.");
                }

                existing.WorkflowId = workflow.Id;
                existing.WorkflowStepOrder = order;
                existing.Status = isFirst ? TaskStatus.InProgress : TaskStatus.Pending;
                if (isFirst)
                {
                    existing.StartDate = DateTime.UtcNow;
                }
                if (!string.IsNullOrWhiteSpace(stepDto.Description))
                {
                    existing.Description = stepDto.Description.Trim();
                }
                stepTask = existing;
            }
            else
            {
                stepTask = new TaskEntity
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Level = request.Level,
                    ProgramId = workflow.ProgramId,
                    ProjectId = workflow.ProjectId ?? Guid.Empty,
                    WorkflowId = workflow.Id,
                    WorkflowStepOrder = order,
                    Title = stepDto.Title.Trim(),
                    Description = stepDto.Description?.Trim() ?? string.Empty,
                    Priority = request.Priority,
                    Status = isFirst ? TaskStatus.InProgress : TaskStatus.Pending,
                    StartDate = isFirst ? DateTime.UtcNow : (stepDto.DueDate ?? DateTime.UtcNow).AddDays(-3),
                    DueDate = stepDto.DueDate ?? DateTime.UtcNow.AddDays(3),
                    ReporterEmail = callerEmail,
                    AssigneeEmails = stepDto.StepOwnerEmail.Trim(),
                    Labels = "WorkflowStep"
                };
                _dbContext.Tasks.Add(stepTask);
            }

            // Process any initial subtasks delegated to team members
            if (stepDto.SubTasks != null && stepDto.SubTasks.Count > 0)
            {
                var subOrder = stepTask.SubTasks.Count + 1;
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

        _dbContext.Workflows.Add(workflow);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Launched workflow '{Title}' ({Id}) at Level {Level}. Constant Owner: {Owner}. Steps: {StepCount}",
            workflow.Title, workflow.Id, workflow.Level, workflow.WorkflowOwnerEmail, workflow.Steps.Count);

        // Notify initial step owner
        var firstStep = workflow.Steps.First();
        await DispatchNotificationSilentlyAsync(
            recipientEmail: firstStep.AssigneeEmails ?? string.Empty,
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

    public async Task<WorkflowEntity> CompleteStepAsync(CompleteWorkflowStepReqDto request, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId ?? throw new InvalidOperationException("Active tenant context is required.");
        var callerEmail = _currentUser.Email ?? string.Empty;

        var workflow = await _dbContext.Workflows
            .Include(w => w.Steps)
                .ThenInclude(s => s.SubTasks)
            .FirstOrDefaultAsync(w => w.Id == request.WorkflowId && w.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException($"Workflow '{request.WorkflowId}' not found in active tenant.");

        var activeStep = workflow.Steps.FirstOrDefault(s => s.Id == request.StepId)
            ?? throw new InvalidOperationException($"Step Task '{request.StepId}' not found in workflow.");

        // Zero Trust Gate 1: Step must be currently active
        if (activeStep.Status != TaskStatus.InProgress || workflow.CurrentStepId != activeStep.Id)
        {
            throw new InvalidOperationException($"Zero Trust Violation: Step '{activeStep.Title}' is not in Active status (Current status: {activeStep.Status}).");
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

        // Complete Active Step
        activeStep.Status = TaskStatus.Closed;
        activeStep.EndDate = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.CompletionNotes))
        {
            _dbContext.TaskComments.Add(new UserTaskCommentEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                TaskId = activeStep.Id,
                ProjectId = workflow.ProjectId ?? Guid.Empty, AuthorUserEntityId = Guid.TryParse(_currentUser.UserId, out var stepAuthorGuid) ? stepAuthorGuid : Guid.Empty, CreatedAtUtc = DateTime.UtcNow,
                CommentText = $"[Step #{activeStep.WorkflowStepOrder} Sign-Off]: {request.CompletionNotes.Trim()}"
            });
        }

        var priorStepOwner = activeStep.AssigneeEmails;
        var nextStep = workflow.Steps
            .Where(s => (s.WorkflowStepOrder ?? 0) > (activeStep.WorkflowStepOrder ?? 0) && s.Status != TaskStatus.Cancelled)
            .OrderBy(s => s.WorkflowStepOrder ?? 0)
            .FirstOrDefault();

        if (nextStep != null)
        {
            // Sequential Handoff to Next Step
            nextStep.Status = TaskStatus.InProgress;
            nextStep.StartDate = DateTime.UtcNow;

            workflow.CurrentStepId = nextStep.Id;

            _logger.LogInformation("Advanced workflow '{Title}' ({Id}) from Step {PriorOrder} ({PriorOwner}) to Step {NextOrder} ({NextOwner})",
                workflow.Title, workflow.Id, activeStep.WorkflowStepOrder, priorStepOwner, nextStep.WorkflowStepOrder, nextStep.AssigneeEmails);

            await _dbContext.SaveChangesAsync(ct);

            // Notify next step owner
            await DispatchNotificationSilentlyAsync(
                recipientEmail: nextStep.AssigneeEmails ?? string.Empty,
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
            var watchers = ParseEmails(workflow.WatcherEmails).Append(priorStepOwner).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var watcher in watchers)
            {
                if (!string.Equals(watcher, nextStep.AssigneeEmails, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(watcher, workflow.WorkflowOwnerEmail, StringComparison.OrdinalIgnoreCase))
                {
                    await DispatchNotificationSilentlyAsync(
                        recipientEmail: watcher!,
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
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var watcher in watchers)
            {
                await DispatchNotificationSilentlyAsync(
                    recipientEmail: watcher!,
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

    public async Task<WorkflowEntity> ReassignStepAsync(ReassignWorkflowStepReqDto request, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId ?? throw new InvalidOperationException("Active tenant context is required.");
        var callerEmail = _currentUser.Email ?? string.Empty;

        var workflow = await _dbContext.Workflows
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

        _dbContext.TaskComments.Add(new UserTaskCommentEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TaskId = targetStep.Id,
            ProjectId = workflow.ProjectId ?? Guid.Empty, AuthorUserEntityId = Guid.TryParse(_currentUser.UserId, out var reassignAuthorGuid) ? reassignAuthorGuid : Guid.Empty, CreatedAtUtc = DateTime.UtcNow,
            CommentText = $"[Step Reassigned]: Reassigned from {priorOwner} to {newOwner}. Reason: {request.Reason}"
        });

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

        var workflow = await _dbContext.Workflows
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

    public async Task<IReadOnlyList<WorkflowEntity>> GetWorkflowsAsync(WorkflowFilterDto filter, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId ?? throw new InvalidOperationException("Active tenant context is required.");

        var query = _dbContext.Workflows
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
                                     (w.AssigneeEmails != null && w.AssigneeEmails.ToLower().Contains(term)) ||
                                     w.Description.ToLower().Contains(term));
        }

        return await query
            .OrderByDescending(w => w.Created)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<WorkflowEntity>> GetProjectWorkflowsAsync(Guid projectId, CancellationToken ct = default)
    {
        return await GetWorkflowsAsync(new WorkflowFilterDto(Level: TaskLevel.Project, ProjectId: projectId), ct);
    }

    public async Task<WorkflowEntity?> GetWorkflowByIdAsync(Guid workflowId, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId ?? throw new InvalidOperationException("Active tenant context is required.");

        return await _dbContext.Workflows
            .Include(w => w.Steps)
                .ThenInclude(s => s.SubTasks)
            .FirstOrDefaultAsync(w => w.Id == workflowId && w.TenantId == tenantId && !w.IsDeleted, ct);
    }

    public async Task<bool> CanUserManageWorkflowAsync(TaskLevel level, Guid? programId, Guid? projectId, CancellationToken ct = default)
    {
        if (_currentUser.IsRootAdmin) return true;
        var tenantId = _tenantContext.TenantId;
        if (string.IsNullOrWhiteSpace(tenantId)) return false;
        var userId = _currentUser.UserId ?? string.Empty;

        return level switch
        {
            TaskLevel.Tenant => await _hierarchyAuth.IsTenantAdminAsync(tenantId, userId, ct),
            TaskLevel.Program => programId.HasValue && (await _hierarchyAuth.IsProgramAdminAsync(programId.Value, userId, ct) || await _hierarchyAuth.IsTenantAdminAsync(tenantId, userId, ct)),
            TaskLevel.Project => projectId.HasValue && (await _hierarchyAuth.IsProjectAdminAsync(projectId.Value, userId, ct) || await _hierarchyAuth.IsTenantAdminAsync(tenantId, userId, ct)),
            _ => await _hierarchyAuth.IsTenantAdminAsync(tenantId, userId, ct)
        };
    }

    public async Task<WorkflowEntity> EditWorkflowAsync(EditWorkflowReqDto request, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId ?? throw new InvalidOperationException("Active tenant context is required.");
        var workflow = await _dbContext.Workflows
            .Include(w => w.Steps)
                .ThenInclude(s => s.SubTasks)
            .FirstOrDefaultAsync(w => w.Id == request.WorkflowId && w.TenantId == tenantId && !w.IsDeleted, ct)
            ?? throw new InvalidOperationException($"Workflow '{request.WorkflowId}' not found.");

        var canManage = await CanUserManageWorkflowAsync(workflow.Level, workflow.ProgramId, workflow.ProjectId, ct);
        if (!canManage)
        {
            _logger.LogWarning("SECURITY ALERT: Caller '{Caller}' denied permission to edit workflow '{WorkflowId}' at {Level} level.",
                _currentUser.Email, workflow.Id, workflow.Level);
            throw new UnauthorizedAccessException($"Zero Trust Violation: Only designated administrators can edit this {workflow.Level} workflow.");
        }

        workflow.Title = request.Title.Trim();
        workflow.Description = request.Description.Trim();
        if (!string.IsNullOrWhiteSpace(request.WorkflowOwnerEmail))
        {
            workflow.WorkflowOwnerEmail = request.WorkflowOwnerEmail.Trim();
        }
        workflow.Priority = request.Priority;
        if (request.DueDate.HasValue)
        {
            workflow.DueDate = request.DueDate.Value;
        }
        workflow.WatcherEmails = request.WatcherEmails?.Trim() ?? string.Empty;

        await _dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("Admin '{Admin}' updated metadata for workflow '{Title}' ({Id})", _currentUser.Email, workflow.Title, workflow.Id);
        return workflow;
    }

    public async Task<WorkflowEntity> CancelStepAsync(CancelWorkflowStepReqDto request, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId ?? throw new InvalidOperationException("Active tenant context is required.");
        var workflow = await _dbContext.Workflows
            .Include(w => w.Steps)
                .ThenInclude(s => s.SubTasks)
            .FirstOrDefaultAsync(w => w.Id == request.WorkflowId && w.TenantId == tenantId && !w.IsDeleted, ct)
            ?? throw new InvalidOperationException($"Workflow '{request.WorkflowId}' not found.");

        var canManage = await CanUserManageWorkflowAsync(workflow.Level, workflow.ProgramId, workflow.ProjectId, ct);
        if (!canManage)
        {
            _logger.LogWarning("SECURITY ALERT: Caller '{Caller}' denied permission to cancel step in workflow '{WorkflowId}'.",
                _currentUser.Email, workflow.Id);
            throw new UnauthorizedAccessException($"Zero Trust Violation: Only designated administrators can cancel steps on this {workflow.Level} workflow.");
        }

        var step = workflow.Steps.FirstOrDefault(s => s.Id == request.StepTaskId)
            ?? throw new InvalidOperationException($"Step task '{request.StepTaskId}' not found in workflow.");

        if (step.Status == TaskStatus.Closed)
        {
            throw new InvalidOperationException("Completed steps cannot be cancelled.");
        }
        if (step.Status == TaskStatus.Cancelled)
        {
            return workflow;
        }

        var wasActive = step.Status == TaskStatus.InProgress;
        step.Status = TaskStatus.Cancelled;
        step.EndDate = DateTime.UtcNow;

        _dbContext.TaskComments.Add(new UserTaskCommentEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TaskId = step.Id,
            ProjectId = workflow.ProjectId ?? Guid.Empty, AuthorUserEntityId = Guid.TryParse(_currentUser.UserId, out var cancelAuthorGuid) ? cancelAuthorGuid : Guid.Empty, CreatedAtUtc = DateTime.UtcNow,
            CommentText = $"[Step Cancelled]: {request.CancellationReason.Trim()}"
        });

        if (wasActive)
        {
            workflow.Status = TaskStatus.Paused;
            workflow.CurrentStepId = null;

            _logger.LogWarning("Active step '{StepTitle}' ({StepId}) on workflow '{Title}' was cancelled. Pipeline is now Paused.",
                step.Title, step.Id, workflow.Title);

            await DispatchNotificationSilentlyAsync(
                recipientEmail: workflow.WorkflowOwnerEmail,
                title: $"Workflow Paused (Attention Required): {workflow.Title}",
                message: $"Active Step #{step.WorkflowStepOrder}: '{step.Title}' was cancelled by {_currentUser.Email} (Reason: {request.CancellationReason}). Pipeline is paused pending admin review.",
                severity: NotificationSeverity.Warning,
                category: NotificationCategory.Personal,
                projectId: workflow.ProjectId,
                ct: ct);
        }

        await _dbContext.SaveChangesAsync(ct);
        return workflow;
    }

    public async Task<WorkflowEntity> ResumePausedWorkflowAsync(ResumePausedWorkflowReqDto request, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId ?? throw new InvalidOperationException("Active tenant context is required.");
        var workflow = await _dbContext.Workflows
            .Include(w => w.Steps)
                .ThenInclude(s => s.SubTasks)
            .FirstOrDefaultAsync(w => w.Id == request.WorkflowId && w.TenantId == tenantId && !w.IsDeleted, ct)
            ?? throw new InvalidOperationException($"Workflow '{request.WorkflowId}' not found.");

        var canManage = await CanUserManageWorkflowAsync(workflow.Level, workflow.ProgramId, workflow.ProjectId, ct);
        if (!canManage)
        {
            throw new UnauthorizedAccessException($"Zero Trust Violation: Only designated administrators can resume this {workflow.Level} workflow.");
        }

        TaskEntity? stepToActivate = null;
        if (request.NextStepTaskId.HasValue && request.NextStepTaskId.Value != Guid.Empty)
        {
            stepToActivate = workflow.Steps.FirstOrDefault(s => s.Id == request.NextStepTaskId.Value && s.Status == TaskStatus.Pending);
        }

        stepToActivate ??= workflow.Steps
            .Where(s => s.Status == TaskStatus.Pending)
            .OrderBy(s => s.WorkflowStepOrder ?? 0)
            .FirstOrDefault();

        if (stepToActivate == null)
        {
            workflow.Status = TaskStatus.Closed;
            workflow.EndDate = DateTime.UtcNow;
            workflow.CurrentStepId = null;
        }
        else
        {
            stepToActivate.Status = TaskStatus.InProgress;
            stepToActivate.StartDate = DateTime.UtcNow;

            workflow.CurrentStepId = stepToActivate.Id;
            workflow.Status = TaskStatus.InProgress;

            await DispatchNotificationSilentlyAsync(
                recipientEmail: stepToActivate.AssigneeEmails ?? string.Empty,
                title: $"Workflow Action Required: {workflow.Title}",
                message: $"Workflow resumed. Step #{stepToActivate.WorkflowStepOrder}: '{stepToActivate.Title}' is now active under your ownership.",
                severity: NotificationSeverity.Warning,
                category: NotificationCategory.Personal,
                projectId: workflow.ProjectId,
                ct: ct);
        }

        await _dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("Admin '{Admin}' resumed workflow '{Title}' ({Id}). Active step: {StepId}",
            _currentUser.Email, workflow.Title, workflow.Id, workflow.CurrentStepId);
        return workflow;
    }

    public async Task<WorkflowEntity> AppendStepAsync(AppendStepToWorkflowReqDto request, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId ?? throw new InvalidOperationException("Active tenant context is required.");
        var workflow = await _dbContext.Workflows
            .Include(w => w.Steps)
                .ThenInclude(s => s.SubTasks)
            .FirstOrDefaultAsync(w => w.Id == request.WorkflowId && w.TenantId == tenantId && !w.IsDeleted, ct)
            ?? throw new InvalidOperationException($"Workflow '{request.WorkflowId}' not found.");

        var canManage = await CanUserManageWorkflowAsync(workflow.Level, workflow.ProgramId, workflow.ProjectId, ct);
        if (!canManage)
        {
            throw new UnauthorizedAccessException($"Zero Trust Violation: Only designated administrators can add steps to this {workflow.Level} workflow.");
        }

        var maxOrder = workflow.Steps.Count > 0 ? workflow.Steps.Max(s => s.WorkflowStepOrder ?? 0) : 0;
        var targetOrder = request.InsertAfterStepOrder.HasValue ? request.InsertAfterStepOrder.Value + 1 : maxOrder + 1;

        if (request.InsertAfterStepOrder.HasValue)
        {
            foreach (var s in workflow.Steps.Where(s => (s.WorkflowStepOrder ?? 0) >= targetOrder))
            {
                s.WorkflowStepOrder = (s.WorkflowStepOrder ?? 0) + 1;
            }
        }

        var callerEmail = !string.IsNullOrWhiteSpace(_currentUser.Email) 
            ? _currentUser.Email 
            : SystemIdentityUtility.ResolveAuditableUserId(_currentUser, "AppendStepToWorkflow");

        TaskEntity stepTask;
        if (request.ExistingTaskId.HasValue && request.ExistingTaskId.Value != Guid.Empty)
        {
            var existing = await _dbContext.Tasks
                .Include(t => t.SubTasks)
                .FirstOrDefaultAsync(t => t.Id == request.ExistingTaskId.Value && t.TenantId == tenantId, ct)
                ?? throw new InvalidOperationException($"Task '{request.ExistingTaskId}' not found.");

            if (existing.WorkflowId.HasValue && existing.WorkflowId != Guid.Empty && existing.WorkflowId != workflow.Id)
            {
                throw new InvalidOperationException($"Task '{existing.Title}' already belongs to another workflow.");
            }

            existing.WorkflowId = workflow.Id;
            existing.WorkflowStepOrder = targetOrder;
            existing.Status = TaskStatus.Pending;
            if (!string.IsNullOrWhiteSpace(request.DeliverableNotes))
            {
                existing.Description = string.IsNullOrWhiteSpace(existing.Description)
                    ? $"Deliverable: {request.DeliverableNotes.Trim()}"
                    : $"{existing.Description}\n\nDeliverable: {request.DeliverableNotes.Trim()}";
            }
            stepTask = existing;
        }
        else
        {
            stepTask = new TaskEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Level = workflow.Level,
                ProgramId = workflow.ProgramId,
                ProjectId = workflow.ProjectId ?? Guid.Empty,
                WorkflowId = workflow.Id,
                WorkflowStepOrder = targetOrder,
                Title = !string.IsNullOrWhiteSpace(request.NewTaskTitle) ? request.NewTaskTitle.Trim() : $"Step #{targetOrder}",
                Description = request.DeliverableNotes?.Trim() ?? string.Empty,
                AssigneeEmails = !string.IsNullOrWhiteSpace(request.NewTaskOwnerEmail) ? request.NewTaskOwnerEmail.Trim() : callerEmail,
                ReporterEmail = callerEmail,
                Priority = workflow.Priority,
                Status = TaskStatus.Pending,
                StartDate = DateTime.UtcNow,
                DueDate = DateTime.UtcNow.AddDays(7),
                Labels = "WorkflowStep"
            };
            _dbContext.Tasks.Add(stepTask);
            workflow.Steps.Add(stepTask);
        }

        await _dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("Admin '{Admin}' added step #{Order} ('{Title}') to workflow '{WorkflowTitle}' ({WorkflowId})",
            _currentUser.Email, targetOrder, stepTask.Title, workflow.Title, workflow.Id);
        return workflow;
    }

    public async Task<IReadOnlyList<TaskEntity>> GetAvailableTasksForWorkflowAsync(TaskSearchFilterDto filter, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId ?? throw new InvalidOperationException("Active tenant context is required.");
        var query = _dbContext.Tasks
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted && t.Status != TaskStatus.Closed && t.Status != TaskStatus.Cancelled);

        if (filter.ExcludeTasksWithWorkflows)
        {
            query = query.Where(t => t.WorkflowId == null || t.WorkflowId == Guid.Empty);
        }

        query = filter.Level switch
        {
            TaskLevel.Project when filter.ProjectId.HasValue => query.Where(t => t.ProjectId == filter.ProjectId.Value),
            TaskLevel.Program when filter.ProgramId.HasValue => query.Where(t => t.ProgramId == filter.ProgramId.Value),
            TaskLevel.Tenant => query.Where(t => t.Level == TaskLevel.Tenant),
            _ => query
        };

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim().ToLowerInvariant();
            query = query.Where(t => t.Title.ToLower().Contains(term) ||
                                     t.Description.ToLower().Contains(term) ||
                                     t.AssigneeEmails.ToLower().Contains(term));
        }

        return await query
            .OrderByDescending(t => t.Created)
            .Take(50)
            .ToListAsync(ct);
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
