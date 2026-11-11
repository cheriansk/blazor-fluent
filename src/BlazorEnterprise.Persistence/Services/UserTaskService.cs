using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Shared.Contracts.Engine.Client;
using BlazorEnterprise.Shared.Contracts.Engine.Transforms;
using BlazorEnterprise.Shared.DataListTypes;
using BlazorEnterprise.Core.Domain.Tasks;
using BlazorEnterprise.Core.Dtos;
using BlazorEnterprise.Core.Dtos.Response;
using BlazorEnterprise.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorEnterprise.Persistence.Services;

public partial class UserTaskService : IUserTaskService
{
    partial void OnBeforeCreateTask(TaskEntity task);
    partial void OnAfterCreateTask(TaskEntity task);

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantContext _tenantContext;
    private readonly INotifyService _notificationService;
    private readonly IProjectAuthorizationService _projectAuth;
    private readonly IHierarchyAuthorizationService _hierarchyAuth;
    private readonly IAuditService _auditService;
    private readonly ILogger<UserTaskService> _logger;
    private readonly IEngineClient? _engineClient;

    public UserTaskService(
        IDbContextFactory<AppDbContext> dbFactory,
        ICurrentUser currentUser,
        ITenantContext tenantContext,
        INotifyService notificationService,
        IProjectAuthorizationService projectAuth,
        IHierarchyAuthorizationService hierarchyAuth,
        IAuditService auditService,
        ILogger<UserTaskService> logger,
        IEngineClient? engineClient = null)
    {
        _dbFactory = dbFactory;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
        _notificationService = notificationService;
        _projectAuth = projectAuth;
        _hierarchyAuth = hierarchyAuth;
        _auditService = auditService;
        _logger = logger;
        _engineClient = engineClient;
    }

    public async Task<IReadOnlyList<TaskEntity>> GetTasksByProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        await _projectAuth.EnsureCanVisitAsync(projectId, operation: "GetTasksByProject", ct: ct);

        var tenantId = _tenantContext.TenantId;

        var tasks = await _context.Tasks
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.ProjectId == projectId)
            .Include(t => t.Comments.Where(c => !c.IsDeleted && c.TenantId == tenantId && c.ProjectId == projectId))
            .OrderBy(t => t.DueDate)
            .ToListAsync(ct);

        await HydrateEncryptedTasksInMemoryAsync(tasks, ct);
        return tasks;
    }

    public async IAsyncEnumerable<TaskEntity> GetTasksByProjectStreamAsync(
        Guid projectId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        await _projectAuth.EnsureCanVisitAsync(projectId, operation: "GetTasksByProjectStream", ct: ct);

        var tenantId = _tenantContext.TenantId;

        var stream = _context.Tasks
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.ProjectId == projectId)
            .Include(t => t.Comments.Where(c => !c.IsDeleted && c.TenantId == tenantId && c.ProjectId == projectId))
            .OrderBy(t => t.DueDate)
            .AsAsyncEnumerable();

        await foreach (var task in stream.WithCancellation(ct))
        {
            await HydrateEncryptedTasksInMemoryAsync([task], ct);
            yield return task;
        }
    }

    public async Task<TaskEntity?> GetTaskByIdAsync(Guid projectId, Guid taskId, CancellationToken ct = default)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        await _projectAuth.EnsureCanVisitAsync(projectId, operation: "GetTaskById", ct: ct);

        var tenantId = _tenantContext.TenantId;

        var task = await _context.Tasks
            .AsNoTracking()
            .Include(t => t.Comments.Where(c => !c.IsDeleted && c.TenantId == tenantId && c.ProjectId == projectId).OrderBy(c => c.CreatedAtUtc))
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.ProjectId == projectId && t.Id == taskId, ct);

        if (task is not null)
        {
            await HydrateEncryptedTasksInMemoryAsync([task], ct);
        }

        return task;
    }

    private async Task HydrateEncryptedTasksInMemoryAsync(IReadOnlyList<TaskEntity> tasks, CancellationToken ct)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        if (_engineClient is null || tasks.Count == 0)
        {
            return;
        }

        var encryptedTasks = tasks
            .Where(t => t.IsEncryptedAtRest && t.EncryptedPayloadBytes is { Length: > 0 })
            .ToList();

        if (encryptedTasks.Count == 0)
        {
            return;
        }

        try
        {
            var req = new DecryptProprietaryReqDto(
                encryptedTasks
                    .Select(t => new DecryptProprietaryItemDto(t.Id, t.EncryptedPayloadBytes!))
                    .ToList());

            var resp = await _engineClient.DecryptProprietaryBatchAsync(req, ct);
            if (!resp.IsSuccess || resp.Data is null || !resp.Data.IsSuccess)
            {
                _logger.LogWarning("On-demand proprietary decryption returned unsuccessful result: {Error}", resp.Error ?? resp.Data?.ErrorMessage);
                return;
            }

            var decryptedById = resp.Data.Items.ToDictionary(i => i.RecordId, i => i.Properties);
            foreach (var task in encryptedTasks)
            {
                if (decryptedById.TryGetValue(task.Id, out var props))
                {
                    if (props.TryGetValue("Title", out var decryptedTitle) && !string.IsNullOrWhiteSpace(decryptedTitle))
                    {
                        task.Title = decryptedTitle;
                    }

                    if (props.TryGetValue("Description", out var decryptedDesc) && decryptedDesc is not null)
                    {
                        task.Description = decryptedDesc;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "DEAD BLOCKER: Failed to hydrate proprietary encrypted tasks in-memory via Engine API.");
        }
    }

    public async Task<Result<TaskEntity>> CreateTaskAsync(TaskEntity task, CancellationToken ct = default)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        try
        {
            if (task.ProjectId.HasValue && task.ProjectId.Value != Guid.Empty)
            {
                await _projectAuth.EnsureCanEditAsync(task.ProjectId.Value, operation: "CreateTask", ct: ct);
            }

            if (task.TenantId == Guid.Empty)
            {
                task.TenantId = _tenantContext.TenantId;
            }

            if (task.ProjectId.HasValue && task.ProjectId.Value != Guid.Empty)
            {
                task.Level = TaskLevelEnum.Project;
                task.ProgramId = null;
            }
            // Automatically stamp creator as reporter
            if (string.IsNullOrWhiteSpace(task.ReporterEmail))
            {
                if (string.IsNullOrWhiteSpace(_currentUser.Email))
                {
                    return Result<TaskEntity>.Failure("Authenticated user email is required to create a task.");
                }
                task.ReporterEmail = _currentUser.Email;
            }

            _context.Tasks.Add(task);
            _context.IsTransactionScopeCommitAuthorized = true; try { await _context.SaveChangesAsync(ct); } finally { _context.IsTransactionScopeCommitAuthorized = false; }

            _logger.LogInformation("Task '{Title}' ({Id}) created in project {ProjectId}", task.Title, task.Id, task.ProjectId);

            // Automated Notification: Alert all assignees
            await NotifyAssigneesAsync(task, isNew: true, ct);

            return Result<TaskEntity>.Success(task);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create task '{Title}'", task.Title);
            return Result<TaskEntity>.Failure($"Failed to create task: {ex.Message}");
        }
    }

    public async Task<Result<TaskEntity>> UpdateTaskAsync(TaskEntity updated, CancellationToken ct = default)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        try
        {
            var tenantId = _tenantContext.TenantId;

            // 1. Compound database lookup: Task MUST match TenantId, ProjectId, and Id to eliminate IDOR
            var task = await _context.Tasks
                .AsTracking()
                .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.ProjectId == updated.ProjectId && t.Id == updated.Id, ct);

            if (task is null)
            {
                return Result<TaskEntity>.Failure("Task not found in the specified project.");
            }

            // 2. Strict Project Immobility: Tasks can NEVER be reassigned or moved across projects
            if (updated.ProjectId != Guid.Empty && updated.ProjectId != task.ProjectId)
            {
                _logger.LogWarning("Security Violation: Attempt to move task {TaskId} from project {OldProject} to {NewProject}",
                    task.Id, task.ProjectId, updated.ProjectId);
                return Result<TaskEntity>.Failure("Cross-project task reassignment is forbidden. Tasks cannot be moved between projects.");
            }

            // 3. Authorize caller against verified database project
            if (task.ProjectId.HasValue && task.ProjectId.Value != Guid.Empty)
            {
                await _projectAuth.EnsureCanEditAsync(task.ProjectId.Value, operation: "UpdateTask", ct: ct);
            }

            task.Title = updated.Title;
            task.Description = updated.Description;
            task.Priority = updated.Priority;
            task.Status = updated.Status;
            task.DueDate = updated.DueDate;
            task.EstimatedHours = updated.EstimatedHours;
            task.AssigneeEmails = updated.AssigneeEmails;
            task.Labels = updated.Labels;
            task.MilestoneId = updated.MilestoneId;

            _context.IsTransactionScopeCommitAuthorized = true; try { await _context.SaveChangesAsync(ct); } finally { _context.IsTransactionScopeCommitAuthorized = false; }
            return Result<TaskEntity>.Success(task);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update task {Id}", updated.Id);
            return Result<TaskEntity>.Failure($"Failed to update task: {ex.Message}");
        }
    }

    public async Task<Result<bool>> CloseTaskAsync(Guid projectId, Guid taskId, CancellationToken ct = default)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        try
        {
            await _projectAuth.EnsureCanEditAsync(projectId, operation: "CloseTask", ct: ct);

            var tenantId = _tenantContext.TenantId;

            var task = await _context.Tasks
                .AsTracking()
                .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.ProjectId == projectId && t.Id == taskId, ct);

            if (task is null) return Result<bool>.Failure("Task not found.");

            task.Status = TaskStatusEnum.Closed;
            task.EndDate = DateTime.UtcNow;

            // Automated Lifecycle Integrity: auto-resolve any WorkDependency contracts where this task was blocking other tasks
            var blockers = await _context.WorkDependencies
                .AsTracking()
                .Where(b => b.TenantId == tenantId && b.DependsOnItemId == taskId && !b.IsDeleted && b.Status != TaskStatusEnum.Closed)
                .ToListAsync(ct);

            foreach (var blocker in blockers)
            {
                blocker.Status = TaskStatusEnum.Closed;
                blocker.EndDate = DateTime.UtcNow;

                if (blocker.DependentItemId.HasValue)
                {
                    var sourceTask = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == blocker.DependentItemId.Value, ct);
                    if (sourceTask != null)
                    {
                        var recipients = sourceTask.GetParsedAssigneeEmails().Concat(blocker.GetParsedWatcherEmails()).Distinct().ToList();
                        foreach (var recipient in recipients)
                        {
                            await SendBlockerAlertAsync(recipient, $"Prerequisite Resolved: '{task.Title}'",
                                $"The prerequisite task '{task.Title}' blocking your task '{sourceTask.Title}' has been closed. Blocker contract is now resolved.",
                                NotificationSeverityEnum.Info, sourceTask.ProjectId ?? Guid.Empty, ct);
                        }
                    }
                }
            }

            _context.IsTransactionScopeCommitAuthorized = true; try { await _context.SaveChangesAsync(ct); } finally { _context.IsTransactionScopeCommitAuthorized = false; }
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to close task {Id}", taskId);
            return Result<bool>.Failure($"Failed to close task: {ex.Message}");
        }
    }

    public async Task<Result<bool>> ReopenTaskAsync(Guid projectId, Guid taskId, CancellationToken ct = default)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        try
        {
            await _projectAuth.EnsureCanEditAsync(projectId, operation: "ReopenTask", ct: ct);

            var tenantId = _tenantContext.TenantId;

            var task = await _context.Tasks
                .AsTracking()
                .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.ProjectId == projectId && t.Id == taskId, ct);

            if (task is null) return Result<bool>.Failure("Task not found.");

            task.Status = TaskStatusEnum.Open;
            task.EndDate = null;

            // Automated Lifecycle Integrity: auto-reopen any WorkDependency contracts where this task was blocking other tasks
            var blockers = await _context.WorkDependencies
                .AsTracking()
                .Where(b => b.TenantId == tenantId && b.DependsOnItemId == taskId && !b.IsDeleted && b.Status == TaskStatusEnum.Closed)
                .ToListAsync(ct);

            foreach (var blocker in blockers)
            {
                blocker.Status = TaskStatusEnum.Open;
                blocker.EndDate = null;

                if (blocker.DependentItemId.HasValue)
                {
                    var sourceTask = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == blocker.DependentItemId.Value, ct);
                    if (sourceTask != null)
                    {
                        var recipients = sourceTask.GetParsedAssigneeEmails().Concat(blocker.GetParsedWatcherEmails()).Distinct().ToList();
                        foreach (var recipient in recipients)
                        {
                            await SendBlockerAlertAsync(recipient, $"Prerequisite Reopened: '{task.Title}'",
                                $"WARNING: Prerequisite task '{task.Title}' blocking '{sourceTask.Title}' was reopened! The blocker is active again.",
                                NotificationSeverityEnum.Warning, sourceTask.ProjectId ?? Guid.Empty, ct);
                        }
                    }
                }
            }

            _context.IsTransactionScopeCommitAuthorized = true; try { await _context.SaveChangesAsync(ct); } finally { _context.IsTransactionScopeCommitAuthorized = false; }
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reopen task {Id}", taskId);
            return Result<bool>.Failure($"Failed to reopen task: {ex.Message}");
        }
    }

    public async Task<Result<bool>> CancelTaskAsync(Guid projectId, Guid taskId, CancellationToken ct = default)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        try
        {
            await _projectAuth.EnsureCanEditAsync(projectId, operation: "CancelTask", ct: ct);

            var tenantId = _tenantContext.TenantId;

            var task = await _context.Tasks
                .AsTracking()
                .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.ProjectId == projectId && t.Id == taskId, ct);

            if (task is null) return Result<bool>.Failure("Task not found.");

            task.Status = TaskStatusEnum.Cancelled;

            _context.IsTransactionScopeCommitAuthorized = true; try { await _context.SaveChangesAsync(ct); } finally { _context.IsTransactionScopeCommitAuthorized = false; }
            _logger.LogInformation("Task '{Title}' ({Id}) marked Cancelled in project {ProjectId}", task.Title, task.Id, projectId);
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel task {Id}", taskId);
            return Result<bool>.Failure($"Failed to cancel task: {ex.Message}");
        }
    }

    public async Task<Result<UserTaskCommentEntity>> AddCommentAsync(Guid projectId, Guid taskId, string commentText, CancellationToken ct = default)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        try
        {
            if (string.IsNullOrWhiteSpace(commentText))
            {
                return Result<UserTaskCommentEntity>.Failure("Comment text cannot be empty.");
            }

            await _projectAuth.EnsureCanEditAsync(projectId, operation: "AddComment", ct: ct);

            var tenantId = _tenantContext.TenantId;

            var task = await _context.Tasks
                .AsTracking()
                .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.ProjectId == projectId && t.Id == taskId, ct);

            if (task is null) return Result<UserTaskCommentEntity>.Failure("Task not found.");

            if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.Email))
            {
                return Result<UserTaskCommentEntity>.Failure("Authenticated user identity is required to add comments.");
            }

            var comment = new UserTaskCommentEntity
            {
                TaskId = taskId,
                ProjectId = projectId,
                TenantId = task.TenantId,
                AuthorUserEntityId = Guid.TryParse(_currentUser.UserId, out var parsedUserGuid) ? parsedUserGuid : (await _context.Users.AsNoTracking().Where(u => u.Email.ToLower() == _currentUser.Email.ToLower() && !u.IsDeleted).Select(u => u.Id).FirstOrDefaultAsync(ct)),


                CommentText = commentText.Trim(),
                CreatedAtUtc = DateTime.UtcNow
            };

            _context.TaskComments.Add(comment);
            _context.IsTransactionScopeCommitAuthorized = true; try { await _context.SaveChangesAsync(ct); } finally { _context.IsTransactionScopeCommitAuthorized = false; }

            _logger.LogInformation("Added comment to task {TaskId} by {Author}", taskId, comment.AuthorName);
            return Result<UserTaskCommentEntity>.Success(comment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add comment to task {TaskId}", taskId);
            return Result<UserTaskCommentEntity>.Failure($"Failed to add comment: {ex.Message}");
        }
    }

    public async Task<IReadOnlyList<UserTaskCommentEntity>> GetCommentsAsync(Guid projectId, Guid taskId, CancellationToken ct = default)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        await _projectAuth.EnsureCanVisitAsync(projectId, operation: "GetComments", ct: ct);

        var tenantId = _tenantContext.TenantId;

        return await _context.TaskComments.Include(c => c.AuthorUser)
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.ProjectId == projectId && c.TaskId == taskId && !c.IsDeleted)
            .OrderBy(c => c.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TaskDependencyItemDto>> GetTaskDependenciesAsync(Guid projectId, Guid taskId, CancellationToken ct = default)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        await _projectAuth.EnsureCanVisitAsync(projectId, operation: "GetTaskDependencies", ct: ct);

        var tenantId = _tenantContext.TenantId;

        var dependencies = await _context.WorkDependencies
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId &&
                        d.ProjectId == projectId &&
                        d.DependentType == DependencyNodeTypeEnum.TaskItem &&
                        d.DependentItemId == taskId &&
                        !d.IsDeleted)
            .ToListAsync(ct);

        if (dependencies.Count == 0) return Array.Empty<TaskDependencyItemDto>();

        var results = new List<TaskDependencyItemDto>();
        foreach (var dep in dependencies)
        {
            string liveTitle = dep.DependsOnSnapshotTitle ?? "Prerequisite Item";
            string liveStatus = dep.DependsOnSnapshotStatus ?? "Open";
            DateTime? liveDueDate = dep.DependsOnSnapshotDueDate ?? dep.DueDate;
            string liveAssignees = dep.AssigneeEmails ?? string.Empty;
            Guid? targetProjectId = dep.DependsOnProjectId;
            Guid? targetProgramId = dep.DependsOnProgramId;
            TaskLevelEnum targetLevel = dep.DependsOnLevel;

            if (dep.DependsOnItemId.HasValue)
            {
                var targetTask = await _context.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == dep.DependsOnItemId.Value, ct);
                if (targetTask != null)
                {
                    liveTitle = targetTask.Title;
                    liveStatus = targetTask.Status.ToString();
                    liveDueDate = targetTask.DueDate;
                    liveAssignees = targetTask.AssigneeEmails ?? string.Empty;
                    targetLevel = targetTask.Level;
                    targetProjectId = targetTask.ProjectId;
                    targetProgramId = targetTask.ProgramId;
                }
                else
                {
                    var targetRisk = await _context.Risks.AsNoTracking().FirstOrDefaultAsync(r => r.Id == dep.DependsOnItemId.Value, ct);
                    if (targetRisk != null)
                    {
                        liveTitle = targetRisk.Title;
                        liveStatus = targetRisk.Status.ToString();
                        liveDueDate = targetRisk.DueDate;
                        liveAssignees = targetRisk.AssigneeEmails ?? string.Empty;
                        targetLevel = targetRisk.Level;
                        targetProjectId = targetRisk.ProjectId;
                        targetProgramId = targetRisk.ProgramId;
                    }
                }
            }

            var canAccess = await _hierarchyAuth.CanViewScopedItemAsync(targetLevel, tenantId, targetProgramId, targetProjectId, ct: ct);

            results.Add(new TaskDependencyItemDto
            {
                DependencyId = dep.Id,
                DependentType = dep.DependentType,
                DependentItemId = dep.DependentItemId,
                DependentLevel = dep.Level,
                ProjectId = dep.ProjectId,
                ProgramId = dep.ProgramId,
                DependsOnType = dep.DependsOnType,
                DependsOnItemId = dep.DependsOnItemId,
                DependsOnLevel = targetLevel,
                DependsOnProjectId = targetProjectId,
                DependsOnProgramId = targetProgramId,
                DependsOnTitle = liveTitle,
                DependsOnStatus = liveStatus,
                DependsOnDueDate = liveDueDate,
                DependsOnAssigneeEmails = liveAssignees,
                Title = dep.Title,
                ImpactDetails = dep.Description ?? string.Empty,
                DependencyType = dep.DependencyType,
                Criticality = dep.Priority,
                Status = dep.Status,
                ResolveByUtc = dep.DueDate,
                IsScheduleConflict = dep.IsScheduleConflict,
                CanAccessTargetItem = canAccess
            });
        }
        return results;
    }

    public async Task<Result<bool>> AddTaskDependencyAsync(
        Guid projectId,
        Guid taskId,
        Guid dependsOnTaskId,
        TaskDependencyTypeEnum dependencyType = TaskDependencyTypeEnum.Blocks,
        DateTime? resolveByUtc = null,
        string? notes = null,
        CancellationToken ct = default)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        try
        {
            if (taskId == dependsOnTaskId)
            {
                return Result<bool>.Failure("A task cannot depend on itself.");
            }

            await _projectAuth.EnsureCanEditAsync(projectId, operation: "AddTaskDependency", ct: ct);

            var tenantId = _tenantContext.TenantId;

            var task = await _context.Tasks
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.ProjectId == projectId && t.Id == taskId, ct);

            if (task == null) return Result<bool>.Failure("Source task not found in the project.");

            var dependsOnTask = await _context.Tasks
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == dependsOnTaskId, ct);

            if (dependsOnTask == null) return Result<bool>.Failure("Prerequisite task not found.");

            if (dependsOnTask.ProjectId != projectId)
            {
                if (dependsOnTask.ProjectId.HasValue && dependsOnTask.ProjectId.Value != Guid.Empty)
                {
                    await _projectAuth.EnsureCanVisitAsync(dependsOnTask.ProjectId.Value, operation: "LinkCrossProjectDependency", ct: ct);
                }
            }

            var exists = await _context.WorkDependencies
                .AnyAsync(d => d.TenantId == tenantId &&
                               d.DependentType == DependencyNodeTypeEnum.TaskItem &&
                               d.DependentItemId == taskId &&
                               d.DependsOnType == DependencyNodeTypeEnum.TaskItem &&
                               d.DependsOnItemId == dependsOnTaskId &&
                               !d.IsDeleted, ct);

            if (exists) return Result<bool>.Failure("This dependency link already exists.");

            var isConflict = dependsOnTask.DueDate.Date > task.DueDate.Date;

            var link = new WorkDependencyEntity
            {
                TenantId = tenantId,
                Level = TaskLevelEnum.Project,
                ProjectId = projectId,
                ProgramId = task.ProgramId,
                DependentType = DependencyNodeTypeEnum.TaskItem,
                DependentItemId = taskId,
                DependsOnType = DependencyNodeTypeEnum.TaskItem,
                DependsOnItemId = dependsOnTaskId,
                DependsOnLevel = TaskLevelEnum.Project,
                DependsOnProjectId = dependsOnTask.ProjectId,
                DependsOnProgramId = dependsOnTask.ProgramId,
                DependsOnSnapshotTitle = dependsOnTask.Title,
                DependsOnSnapshotStatus = dependsOnTask.Status.ToString(),
                DependsOnSnapshotDueDate = dependsOnTask.DueDate,
                IsScheduleConflict = isConflict,
                DependencyType = dependencyType,
                Title = $"Task Dependency: '{task.Title}' depends on '{dependsOnTask.Title}'",
                Description = notes ?? $"Prerequisite task dependency on '{dependsOnTask.Title}'",
                Priority = task.Priority,
                Status = dependsOnTask.IsClosed ? TaskStatusEnum.Closed : TaskStatusEnum.Open,
                StartDate = DateTime.UtcNow,
                DueDate = resolveByUtc ?? dependsOnTask.DueDate,
                EndDate = dependsOnTask.IsClosed ? DateTime.UtcNow : null,
                ReporterEmail = _currentUser.Email ?? task.ReporterEmail,
                AssigneeEmails = dependsOnTask.AssigneeEmails,
                WatcherEmails = task.WatcherEmails ?? string.Empty
            };

            _context.WorkDependencies.Add(link);
            _context.IsTransactionScopeCommitAuthorized = true; try { await _context.SaveChangesAsync(ct); } finally { _context.IsTransactionScopeCommitAuthorized = false; }

            _logger.LogInformation("Task dependency created: Task {TaskId} -> {DependsOnId} ({Type})", taskId, dependsOnTaskId, dependencyType);

            await _auditService.LogUserActivityAsync(
                $"Established task dependency: Task '{task.Title}' {dependencyType} Prerequisite '{dependsOnTask.Title}'",
                $"ProjectId: {projectId}, TaskId: {taskId}, DependsOnTaskId: {dependsOnTaskId}, Type: {dependencyType}, Conflict: {isConflict}",
                ct);

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add task dependency for task {TaskId}", taskId);
            return Result<bool>.Failure($"Failed to add task dependency: {ex.Message}");
        }
    }

    public async Task<Result<bool>> RemoveTaskDependencyAsync(Guid projectId, Guid dependencyId, CancellationToken ct = default)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        try
        {
            await _projectAuth.EnsureCanEditAsync(projectId, operation: "RemoveTaskDependency", ct: ct);

            var tenantId = _tenantContext.TenantId;

            var link = await _context.WorkDependencies
                .AsTracking()
                .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.ProjectId == projectId && d.Id == dependencyId && !d.IsDeleted, ct);

            if (link == null) return Result<bool>.Failure("Dependency not found.");

            link.IsDeleted = true;
            link.DeletedAtUtc = DateTime.UtcNow;
            link.DeletedBy = _currentUser.Email ?? _currentUser.UserId;

            _context.IsTransactionScopeCommitAuthorized = true; try { await _context.SaveChangesAsync(ct); } finally { _context.IsTransactionScopeCommitAuthorized = false; }

            _logger.LogInformation("Task dependency {DependencyId} removed in project {ProjectId}", dependencyId, projectId);

            await _auditService.LogUserActivityAsync(
                $"Removed task dependency link ({link.DependencyType}) for Task {link.DependentItemId} and Prerequisite {link.DependsOnItemId}",
                $"ProjectId: {projectId}, DependencyId: {dependencyId}, DependentItemId: {link.DependentItemId}, DependsOnItemId: {link.DependsOnItemId}",
                ct);

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove task dependency {DependencyId}", dependencyId);
            return Result<bool>.Failure($"Failed to remove task dependency: {ex.Message}");
        }
    }

    private async Task NotifyAssigneesAsync(TaskEntity task, bool isNew, CancellationToken ct)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        var emails = task.GetParsedAssigneeEmails();
        if (emails.Count == 0) return;

        var dueDateFormatted = task.DueDate.ToString("yyyy-MM-dd");
        var title = isNew ? $"New Task Assigned: {task.Title}" : $"Task Updated: {task.Title}";
        var message = $"You have been assigned to task '{task.Title}'. Priority: {task.Priority}, Due: {dueDateFormatted}.";
        var linkUrl = $"/projects/{task.ProjectId}/tasks";

        // Batch query all target users in a single round-trip instead of N queries
        var normalizedEmails = emails.Select(e => e.Trim().ToLowerInvariant()).Distinct().ToList();
        var targetUsers = await _context.Users
            .AsNoTracking()
            .Where(u => normalizedEmails.Contains(u.Email.ToLower()))
            .ToDictionaryAsync(u => u.Email.ToLower(), u => u.Id.ToString(), ct);

        foreach (var email in emails)
        {
            try
            {
                var norm = email.Trim().ToLowerInvariant();
                var targetUserId = targetUsers.TryGetValue(norm, out var uid) ? uid : email;

                var request = new SendNotificationReqDto
                {
                    Category = NotificationCategoryEnum.Personal,
                    Severity = task.Priority == TaskPriorityEnum.Urgent ? NotificationSeverityEnum.Warning : NotificationSeverityEnum.Info,
                    Title = title,
                    Message = message,
                    ProjectId = task.ProjectId ?? Guid.Empty,
                    UserId = targetUserId,
                    LinkUrl = linkUrl,
                    Channels = NotificationChannelsEnum.InApp | NotificationChannelsEnum.Email
                };

                await _notificationService.SendAsync(request, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to dispatch in-app notification to assignee {Email} for task {TaskId}", email, task.Id);
            }
        }
    }

    // --- SubTasks Operations ---

    public async Task<IReadOnlyList<SubTaskEntity>> GetSubTasksByTaskIdAsync(Guid taskId, CancellationToken ct = default)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        var tenantId = _tenantContext.TenantId;
        return await _context.SubTasks
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.ParentTaskId == taskId && !s.IsDeleted)
            .OrderBy(s => s.OrderIndex)
            .ThenBy(s => s.Created)
            .ToListAsync(ct);
    }

    public async Task<Result<SubTaskEntity>> CreateSubTaskAsync(SubTaskEntity subTask, CancellationToken ct = default)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        try
        {
            var tenantId = _tenantContext.TenantId;
            var parentTask = await _context.Tasks.FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == subTask.ParentTaskId, ct);
            if (parentTask == null) return Result<SubTaskEntity>.Failure("Parent task does not exist.");

            if (parentTask.ProjectId != Guid.Empty)
            {
                if (parentTask.ProjectId.HasValue && parentTask.ProjectId.Value != Guid.Empty)
                {
                    await _projectAuth.EnsureCanEditAsync(parentTask.ProjectId.Value, operation: "CreateSubTask", ct: ct);
                }
            }

            subTask.TenantId = tenantId;
            subTask.ProgramId = parentTask.ProgramId;
            subTask.ProjectId = parentTask.ProjectId;
            subTask.Level = parentTask.Level;
            if (string.IsNullOrWhiteSpace(subTask.ReporterEmail))
            {
                subTask.ReporterEmail = _currentUser.Email ?? parentTask.ReporterEmail;
            }

            await _context.SubTasks.AddAsync(subTask, ct);
            _context.IsTransactionScopeCommitAuthorized = true; try { await _context.SaveChangesAsync(ct); } finally { _context.IsTransactionScopeCommitAuthorized = false; }
            return Result<SubTaskEntity>.Success(subTask);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create subtask for Task {TaskId}", subTask.ParentTaskId);
            return Result<SubTaskEntity>.Failure($"Failed to create subtask: {ex.Message}");
        }
    }

    public async Task<Result<SubTaskEntity>> UpdateSubTaskAsync(SubTaskEntity updated, CancellationToken ct = default)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        try
        {
            var tenantId = _tenantContext.TenantId;
            var subTask = await _context.SubTasks.AsTracking()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == updated.Id, ct);
            if (subTask == null) return Result<SubTaskEntity>.Failure("Subtask not found.");

            if (subTask.ProjectId.HasValue && subTask.ProjectId.Value != Guid.Empty)
            {
                await _projectAuth.EnsureCanEditAsync(subTask.ProjectId.Value, operation: "UpdateSubTask", ct: ct);
            }

            subTask.Title = updated.Title;
            subTask.Description = updated.Description;
            subTask.Priority = updated.Priority;
            subTask.Status = updated.Status;
            subTask.DueDate = updated.DueDate;
            subTask.AssigneeEmails = updated.AssigneeEmails;
            subTask.OrderIndex = updated.OrderIndex;
            subTask.BlockedBySubTaskId = updated.BlockedBySubTaskId;

            _context.IsTransactionScopeCommitAuthorized = true; try { await _context.SaveChangesAsync(ct); } finally { _context.IsTransactionScopeCommitAuthorized = false; }
            return Result<SubTaskEntity>.Success(subTask);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update subtask {Id}", updated.Id);
            return Result<SubTaskEntity>.Failure($"Failed to update subtask: {ex.Message}");
        }
    }

    public async Task<Result<bool>> DeleteSubTaskAsync(Guid subTaskId, CancellationToken ct = default)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        try
        {
            var tenantId = _tenantContext.TenantId;
            var subTask = await _context.SubTasks.AsTracking()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == subTaskId, ct);
            if (subTask == null) return Result<bool>.Failure("Subtask not found.");

            if (subTask.ProjectId.HasValue && subTask.ProjectId.Value != Guid.Empty)
            {
                await _projectAuth.EnsureCanEditAsync(subTask.ProjectId.Value, operation: "DeleteSubTask", ct: ct);
            }

            subTask.IsDeleted = true;
            subTask.DeletedAtUtc = DateTime.UtcNow;
            subTask.DeletedBy = _currentUser.Email ?? _currentUser.UserId;

            _context.IsTransactionScopeCommitAuthorized = true; try { await _context.SaveChangesAsync(ct); } finally { _context.IsTransactionScopeCommitAuthorized = false; }
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete subtask {Id}", subTaskId);
            return Result<bool>.Failure($"Failed to delete subtask: {ex.Message}");
        }
    }

    // --- Blocker Dependency & Link Operations ---

    public async Task<IReadOnlyList<WorkDependencyEntity>> GetTaskLinksAsync(Guid taskId, CancellationToken ct = default)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        var tenantId = _tenantContext.TenantId;
        return await _context.WorkDependencies
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId &&
                        ((d.DependentType == DependencyNodeTypeEnum.TaskItem && d.DependentItemId == taskId) ||
                         (d.DependsOnType == DependencyNodeTypeEnum.TaskItem && d.DependsOnItemId == taskId)) &&
                        !d.IsDeleted)
            .ToListAsync(ct);
    }

    public async Task<Result<WorkDependencyEntity>> CreateBlockedByLinkAsync(
        Guid sourceTaskId,
        Guid targetTaskId,
        DateTime resolveDueDate,
        DependencyCriticalityEnum criticality,
        string impactDetails,
        string? watcherEmails = null,
        CancellationToken ct = default)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        try
        {
            if (sourceTaskId == targetTaskId)
            {
                return Result<WorkDependencyEntity>.Failure("A task cannot block itself.");
            }

            var tenantId = _tenantContext.TenantId;

            var sourceTask = await _context.Tasks.FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == sourceTaskId, ct);
            if (sourceTask == null) return Result<WorkDependencyEntity>.Failure("Source task not found.");

            var targetTask = await _context.Tasks.FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == targetTaskId, ct);
            if (targetTask == null) return Result<WorkDependencyEntity>.Failure("Target task (prerequisite) not found.");

            if (sourceTask.ProjectId != targetTask.ProjectId)
            {
                if (targetTask.ProjectId.HasValue && targetTask.ProjectId.Value != Guid.Empty)
                {
                    await _projectAuth.EnsureCanVisitAsync(targetTask.ProjectId.Value, operation: "LinkCrossProjectBlocker", ct: ct);
                }
            }

            var existingLink = await _context.WorkDependencies.FirstOrDefaultAsync(l =>
                l.TenantId == tenantId &&
                l.DependentType == DependencyNodeTypeEnum.TaskItem &&
                l.DependentItemId == sourceTaskId &&
                l.DependsOnType == DependencyNodeTypeEnum.TaskItem &&
                l.DependsOnItemId == targetTaskId &&
                !l.IsDeleted, ct);

            if (existingLink != null)
            {
                return Result<WorkDependencyEntity>.Failure("A link between these two tasks already exists.");
            }

            var isConflict = targetTask.DueDate.Date > sourceTask.DueDate.Date;

            var blocker = new WorkDependencyEntity
            {
                TenantId = tenantId,
                Level = sourceTask.Level,
                ProjectId = sourceTask.ProjectId,
                ProgramId = sourceTask.ProgramId,
                DependentType = DependencyNodeTypeEnum.TaskItem,
                DependentItemId = sourceTaskId,
                DependsOnType = DependencyNodeTypeEnum.TaskItem,
                DependsOnItemId = targetTaskId,
                DependsOnLevel = targetTask.Level,
                DependsOnProjectId = targetTask.ProjectId,
                DependsOnProgramId = targetTask.ProgramId,
                DependsOnSnapshotTitle = targetTask.Title,
                DependsOnSnapshotStatus = targetTask.Status.ToString(),
                DependsOnSnapshotDueDate = targetTask.DueDate,
                IsScheduleConflict = isConflict,
                Title = $"Blocker: '{sourceTask.Title}' blocked by '{targetTask.Title}'",
                Description = string.IsNullOrWhiteSpace(impactDetails)
                    ? $"Dependency contract. Prerequisite: '{targetTask.Title}'. Dependent: '{sourceTask.Title}'."
                    : impactDetails,
                Priority = criticality switch
                {
                    DependencyCriticalityEnum.Low => TaskPriorityEnum.Low,
                    DependencyCriticalityEnum.Medium => TaskPriorityEnum.Medium,
                    DependencyCriticalityEnum.High => TaskPriorityEnum.High,
                    DependencyCriticalityEnum.Critical => TaskPriorityEnum.Urgent,
                    _ => TaskPriorityEnum.Medium
                },
                Criticality = criticality,
                Status = targetTask.IsClosed ? TaskStatusEnum.Closed : TaskStatusEnum.Open,
                StartDate = DateTime.UtcNow,
                DueDate = resolveDueDate,
                EndDate = targetTask.IsClosed ? DateTime.UtcNow : null,
                ReporterEmail = _currentUser.Email ?? sourceTask.ReporterEmail,
                AssigneeEmails = targetTask.AssigneeEmails ?? targetTask.ReporterEmail,
                WatcherEmails = watcherEmails ?? string.Empty,
                DependencyType = TaskDependencyTypeEnum.BlockedBy
            };

            await _context.WorkDependencies.AddAsync(blocker, ct);
            _context.IsTransactionScopeCommitAuthorized = true; try { await _context.SaveChangesAsync(ct); } finally { _context.IsTransactionScopeCommitAuthorized = false; }

            // Notify prerequisite assignee about the new blocker SLA
            var notifyEmails = blocker.GetParsedAssigneeEmails();
            foreach (var email in notifyEmails)
            {
                await SendBlockerAlertAsync(email, $"New Blocker Dependency Assigned: '{targetTask.Title}'",
                    $"Your task '{targetTask.Title}' is blocking '{sourceTask.Title}'. Resolve SLA: {resolveDueDate:yyyy-MM-dd}. Criticality: {criticality}.",
                    NotificationSeverityEnum.Warning, targetTask.ProjectId ?? Guid.Empty, ct);
            }

            return Result<WorkDependencyEntity>.Success(blocker);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create blocked-by link from {Source} to {Target}", sourceTaskId, targetTaskId);
            return Result<WorkDependencyEntity>.Failure($"Failed to create blocker link: {ex.Message}");
        }
    }

    public async Task<Result<bool>> RemoveTaskLinkAsync(Guid linkId, CancellationToken ct = default)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        try
        {
            var tenantId = _tenantContext.TenantId;
            var link = await _context.WorkDependencies.AsTracking()
                .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.Id == linkId && !l.IsDeleted, ct);

            if (link == null) return Result<bool>.Failure("Link not found.");

            link.IsDeleted = true;
            link.DeletedAtUtc = DateTime.UtcNow;
            link.DeletedBy = _currentUser.Email ?? _currentUser.UserId;

            _context.IsTransactionScopeCommitAuthorized = true; try { await _context.SaveChangesAsync(ct); } finally { _context.IsTransactionScopeCommitAuthorized = false; }
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove link {LinkId}", linkId);
            return Result<bool>.Failure($"Failed to remove link: {ex.Message}");
        }
    }

    public async Task<Result<WorkDependencyEntity>> PromoteManualDependencyAsync(
        Guid dependencyId,
        DependencyNodeTypeEnum targetType,
        Guid targetItemId,
        Guid? targetProjectId,
        Guid? targetProgramId,
        CancellationToken ct = default)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        try
        {
            var tenantId = _tenantContext.TenantId;
            var dep = await _context.WorkDependencies.AsTracking()
                .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Id == dependencyId && !d.IsDeleted, ct);

            if (dep == null) return Result<WorkDependencyEntity>.Failure("Dependency not found.");

            dep.DependsOnType = targetType;
            dep.DependsOnItemId = targetItemId;
            dep.DependsOnProjectId = targetProjectId;
            dep.DependsOnProgramId = targetProgramId;

            if (targetType == DependencyNodeTypeEnum.TaskItem)
            {
                var task = await _context.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == targetItemId, ct);
                if (task != null)
                {
                    dep.DependsOnSnapshotTitle = task.Title;
                    dep.DependsOnSnapshotStatus = task.Status.ToString();
                    dep.DependsOnSnapshotDueDate = task.DueDate;
                    dep.IsScheduleConflict = task.DueDate.Date > dep.DueDate.Date;
                }
            }
            else if (targetType == DependencyNodeTypeEnum.CalendarGate)
            {
                var cal = await _context.Calendars.AsNoTracking().FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == targetItemId, ct);
                if (cal != null)
                {
                    dep.DependsOnSnapshotTitle = cal.Title;
                    dep.DependsOnSnapshotStatus = cal.Status.ToString();
                    dep.DependsOnSnapshotDueDate = cal.EndDate ?? cal.StartDate;
                    dep.IsScheduleConflict = (cal.EndDate ?? cal.StartDate).Date > dep.DueDate.Date;
                }
            }

            _context.IsTransactionScopeCommitAuthorized = true; try { await _context.SaveChangesAsync(ct); } finally { _context.IsTransactionScopeCommitAuthorized = false; }
            return Result<WorkDependencyEntity>.Success(dep);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to promote manual dependency {Id}", dependencyId);
            return Result<WorkDependencyEntity>.Failure($"Failed to promote dependency: {ex.Message}");
        }
    }

    private async Task SendBlockerAlertAsync(string email, string title, string message, NotificationSeverityEnum severity, Guid projectId, CancellationToken ct)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        try
        {
            var norm = email.Trim().ToLowerInvariant();
            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email.ToLower() == norm, ct);
            var userId = user != null ? user.Id.ToString() : email;

            var request = new SendNotificationReqDto
            {
                Category = NotificationCategoryEnum.Personal,
                Severity = severity,
                Title = title,
                Message = message,
                ProjectId = projectId,
                UserId = userId,
                Channels = NotificationChannelsEnum.InApp | NotificationChannelsEnum.Email
            };

            await _notificationService.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send blocker alert to {Email}", email);
        }
    }

    public async Task<IReadOnlyList<TaskEntity>> GetTenantTasksAsync(Guid tenantId, CancellationToken ct = default)
    {
        await _hierarchyAuth.EnsureIsTenantAdminAsync(tenantId, ct: ct);
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        return await _context.Tasks
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.Level == TaskLevelEnum.Tenant && !t.IsDeleted)
            .OrderByDescending(t => t.Created)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<RiskEntity>> GetTenantRisksAsync(Guid tenantId, CancellationToken ct = default)
    {
        await _hierarchyAuth.EnsureIsTenantAdminAsync(tenantId, ct: ct);
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        return await _context.Risks
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.Level == TaskLevelEnum.Tenant && !r.IsDeleted)
            .OrderByDescending(r => r.Created)
            .ToListAsync(ct);
    }

    public async Task<Result<RiskEntity>> CreateRiskAsync(RiskEntity risk, CancellationToken ct = default)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        try
        {
            _context.Risks.Add(risk);
            _context.IsTransactionScopeCommitAuthorized = true; try { await _context.SaveChangesAsync(ct); } finally { _context.IsTransactionScopeCommitAuthorized = false; }
            return Result<RiskEntity>.Success(risk);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create risk: {Title}", risk.Title);
            return Result<RiskEntity>.Failure(ex.Message);
        }
    }

    public async Task<IReadOnlyList<TaskEntity>> GetRecentTasksAsync(int limit = 30, CancellationToken ct = default)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        var tenantId = _tenantContext.TenantId;
        var candidates = await _context.Tasks
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted)
            .OrderByDescending(t => t.Created)
            .Take(limit * 2)
            .ToListAsync(ct);

        var authorized = new List<TaskEntity>();
        foreach (var task in candidates)
        {
            if (await _hierarchyAuth.CanViewScopedItemAsync(task.Level, task.TenantId, task.ProgramId, task.ProjectId, ct: ct))
            {
                authorized.Add(task);
                if (authorized.Count >= limit) break;
            }
        }
        return authorized;
    }

    public async Task<IReadOnlyList<WorkDependencyEntity>> GetProjectWorkDependenciesAsync(Guid projectId, CancellationToken ct = default)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        return await _context.WorkDependencies
            .AsNoTracking()
            .Where(d => d.ProjectId == projectId && !d.IsDeleted)
            .ToListAsync(ct);
    }
}
