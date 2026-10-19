using BlazorFluent.Core.Contracts;
using BlazorFluent.Shared.Contracts.Engine.Client;
using BlazorFluent.Shared.Contracts.Engine.Transforms;
using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Core.Domain.Tasks;
using BlazorFluent.Core.Dtos;
using BlazorFluent.Core.Dtos.Response;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Persistence.Services;

public partial class UserTaskService : IUserTaskService
{
    partial void OnBeforeCreateTask(TaskEntity task);
    partial void OnAfterCreateTask(TaskEntity task);

    private readonly AppDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantContext _tenantContext;
    private readonly INotifyService _notificationService;
    private readonly IProjectAuthorizationService _projectAuth;
    private readonly ILogger<UserTaskService> _logger;
    private readonly IEngineClient? _engineClient;

    public UserTaskService(
        AppDbContext context,
        ICurrentUser currentUser,
        ITenantContext tenantContext,
        INotifyService notificationService,
        IProjectAuthorizationService projectAuth,
        ILogger<UserTaskService> logger,
        IEngineClient? engineClient = null)
    {
        _context = context;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
        _notificationService = notificationService;
        _projectAuth = projectAuth;
        _logger = logger;
        _engineClient = engineClient;
    }

    public async Task<IReadOnlyList<TaskEntity>> GetTasksByProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        await _projectAuth.EnsureCanVisitAsync(projectId, operation: "GetTasksByProject", ct: ct);

        var tenantId = _tenantContext.TenantId ?? string.Empty;

        var tasks = await _context.Tasks
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.ProjectId == projectId)
            .Include(t => t.Comments.Where(c => !c.IsDeleted && c.TenantId == tenantId && c.ProjectId == projectId))
            .OrderBy(t => t.DueDate)
            .ToListAsync(ct);

        await HydrateEncryptedTasksInMemoryAsync(tasks, ct);
        return tasks;
    }

    public async Task<TaskEntity?> GetTaskByIdAsync(Guid projectId, Guid taskId, CancellationToken ct = default)
    {
        await _projectAuth.EnsureCanVisitAsync(projectId, operation: "GetTaskById", ct: ct);

        var tenantId = _tenantContext.TenantId ?? string.Empty;

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
            _logger.LogWarning(ex, "Failed to hydrate proprietary encrypted tasks in-memory via Engine API.");
        }
    }

    public async Task<Result<TaskEntity>> CreateTaskAsync(TaskEntity task, CancellationToken ct = default)
    {
        try
        {
            await _projectAuth.EnsureCanEditAsync(task.ProjectId, operation: "CreateTask", ct: ct);

            if (string.IsNullOrWhiteSpace(task.TenantId))
            {
                task.TenantId = _tenantContext.TenantId ?? string.Empty;
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
            await _context.SaveChangesAsync(ct);

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
        try
        {
            var tenantId = _tenantContext.TenantId ?? string.Empty;

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
            await _projectAuth.EnsureCanEditAsync(task.ProjectId, operation: "UpdateTask", ct: ct);

            task.Title = updated.Title;
            task.Description = updated.Description;
            task.Priority = updated.Priority;
            task.Status = updated.Status;
            task.DueDate = updated.DueDate;
            task.AssigneeEmails = updated.AssigneeEmails;
            task.Labels = updated.Labels;
            task.MilestoneId = updated.MilestoneId;

            await _context.SaveChangesAsync(ct);
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
        try
        {
            await _projectAuth.EnsureCanEditAsync(projectId, operation: "CloseTask", ct: ct);

            var tenantId = _tenantContext.TenantId ?? string.Empty;

            var task = await _context.Tasks
                .AsTracking()
                .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.ProjectId == projectId && t.Id == taskId, ct);

            if (task is null) return Result<bool>.Failure("Task not found.");

            task.Status = TaskStatus.Closed;
            task.EndDate = DateTime.UtcNow;

            // Automated Lifecycle Integrity: auto-resolve any BlockerDependency contracts where this task was blocking other tasks
            var blockers = await _context.BlockerDependencies
                .AsTracking()
                .Where(b => b.TenantId == tenantId && b.TargetTaskId == taskId && !b.IsDeleted && b.Status != TaskStatus.Closed)
                .ToListAsync(ct);

            foreach (var blocker in blockers)
            {
                blocker.Status = TaskStatus.Closed;
                blocker.EndDate = DateTime.UtcNow;

                var sourceTask = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == blocker.SourceTaskId, ct);
                if (sourceTask != null)
                {
                    var recipients = sourceTask.GetParsedAssigneeEmails().Concat(blocker.GetParsedWatcherEmails()).Distinct().ToList();
                    foreach (var recipient in recipients)
                    {
                        await SendBlockerAlertAsync(recipient, $"Prerequisite Resolved: '{task.Title}'",
                            $"The prerequisite task '{task.Title}' blocking your task '{sourceTask.Title}' has been closed. Blocker contract is now resolved.",
                            NotificationSeverity.Info, sourceTask.ProjectId, ct);
                    }
                }
            }

            await _context.SaveChangesAsync(ct);
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
        try
        {
            await _projectAuth.EnsureCanEditAsync(projectId, operation: "ReopenTask", ct: ct);

            var tenantId = _tenantContext.TenantId ?? string.Empty;

            var task = await _context.Tasks
                .AsTracking()
                .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.ProjectId == projectId && t.Id == taskId, ct);

            if (task is null) return Result<bool>.Failure("Task not found.");

            task.Status = TaskStatus.Open;
            task.EndDate = null;

            // Automated Lifecycle Integrity: auto-reopen any BlockerDependency contracts where this task was blocking other tasks
            var blockers = await _context.BlockerDependencies
                .AsTracking()
                .Where(b => b.TenantId == tenantId && b.TargetTaskId == taskId && !b.IsDeleted && b.Status == TaskStatus.Closed)
                .ToListAsync(ct);

            foreach (var blocker in blockers)
            {
                blocker.Status = TaskStatus.Open;
                blocker.EndDate = null;

                var sourceTask = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == blocker.SourceTaskId, ct);
                if (sourceTask != null)
                {
                    var recipients = sourceTask.GetParsedAssigneeEmails().Concat(blocker.GetParsedWatcherEmails()).Distinct().ToList();
                    foreach (var recipient in recipients)
                    {
                        await SendBlockerAlertAsync(recipient, $"Prerequisite Reopened: '{task.Title}'",
                            $"WARNING: Prerequisite task '{task.Title}' blocking '{sourceTask.Title}' was reopened! The blocker is active again.",
                            NotificationSeverity.Warning, sourceTask.ProjectId, ct);
                    }
                }
            }

            await _context.SaveChangesAsync(ct);
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
        try
        {
            await _projectAuth.EnsureCanEditAsync(projectId, operation: "CancelTask", ct: ct);

            var tenantId = _tenantContext.TenantId ?? string.Empty;

            var task = await _context.Tasks
                .AsTracking()
                .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.ProjectId == projectId && t.Id == taskId, ct);

            if (task is null) return Result<bool>.Failure("Task not found.");

            task.Status = TaskStatus.Cancelled;

            await _context.SaveChangesAsync(ct);
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
        try
        {
            if (string.IsNullOrWhiteSpace(commentText))
            {
                return Result<UserTaskCommentEntity>.Failure("Comment text cannot be empty.");
            }

            await _projectAuth.EnsureCanEditAsync(projectId, operation: "AddComment", ct: ct);

            var tenantId = _tenantContext.TenantId ?? string.Empty;

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
                AuthorUserId = _currentUser.UserId ?? _currentUser.Email,
                AuthorName = !string.IsNullOrWhiteSpace(_currentUser.UserName) ? _currentUser.UserName : _currentUser.Email,
                AuthorEmail = _currentUser.Email,
                CommentText = commentText.Trim(),
                CreatedAtUtc = DateTime.UtcNow
            };

            _context.TaskComments.Add(comment);
            await _context.SaveChangesAsync(ct);

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
        await _projectAuth.EnsureCanVisitAsync(projectId, operation: "GetComments", ct: ct);

        var tenantId = _tenantContext.TenantId ?? string.Empty;

        return await _context.TaskComments
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.ProjectId == projectId && c.TaskId == taskId && !c.IsDeleted)
            .OrderBy(c => c.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TaskDependencyItemDto>> GetTaskDependenciesAsync(Guid projectId, Guid taskId, CancellationToken ct = default)
    {
        await _projectAuth.EnsureCanVisitAsync(projectId, operation: "GetTaskDependencies", ct: ct);

        var tenantId = _tenantContext.TenantId ?? string.Empty;

        var dependencies = await _context.TaskDependencies
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.ProjectId == projectId && d.TaskId == taskId)
            .ToListAsync(ct);

        if (dependencies.Count == 0) return Array.Empty<TaskDependencyItemDto>();

        var dependsOnIds = dependencies.Select(d => d.DependsOnTaskId).Distinct().ToList();
        var targetTasks = await _context.Tasks
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.ProjectId == projectId && dependsOnIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, ct);

        var result = new List<TaskDependencyItemDto>();
        foreach (var dep in dependencies)
        {
            targetTasks.TryGetValue(dep.DependsOnTaskId, out var targetTask);
            result.Add(new TaskDependencyItemDto
            {
                DependencyId = dep.Id,
                TaskId = dep.TaskId,
                DependsOnTaskId = dep.DependsOnTaskId,
                DependsOnTaskTitle = targetTask?.Title ?? "Unknown Task",
                DependsOnTaskStatus = targetTask?.Status ?? TaskStatus.Open,
                DependsOnAssigneeEmails = targetTask?.AssigneeEmails ?? string.Empty,
                DependsOnDueDate = targetTask?.DueDate,
                DependencyType = dep.DependencyType,
                ResolveByUtc = dep.ResolveByUtc
            });
        }

        return result;
    }

    public async Task<Result<bool>> AddTaskDependencyAsync(
        Guid projectId,
        Guid taskId,
        Guid dependsOnTaskId,
        TaskDependencyType dependencyType = TaskDependencyType.Blocks,
        DateTime? resolveByUtc = null,
        string? notes = null,
        CancellationToken ct = default)
    {
        try
        {
            if (taskId == dependsOnTaskId)
            {
                return Result<bool>.Failure("A task cannot depend on itself.");
            }

            await _projectAuth.EnsureCanEditAsync(projectId, operation: "AddTaskDependency", ct: ct);

            var tenantId = _tenantContext.TenantId ?? string.Empty;

            var task = await _context.Tasks
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.ProjectId == projectId && t.Id == taskId, ct);

            if (task == null) return Result<bool>.Failure("Source task not found in the project.");

            var dependsOnTask = await _context.Tasks
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.ProjectId == projectId && t.Id == dependsOnTaskId, ct);

            if (dependsOnTask == null) return Result<bool>.Failure("Prerequisite task not found in the project.");

            var exists = await _context.TaskDependencies
                .AnyAsync(d => d.TenantId == tenantId && d.ProjectId == projectId && d.TaskId == taskId && d.DependsOnTaskId == dependsOnTaskId, ct);

            if (exists) return Result<bool>.Failure("This dependency link already exists.");

            var link = new TaskDependencyEntity
            {
                TenantId = tenantId,
                ProjectId = projectId,
                TaskId = taskId,
                DependsOnTaskId = dependsOnTaskId,
                DependencyType = dependencyType,
                ResolveByUtc = resolveByUtc,
                Notes = notes
            };

            _context.TaskDependencies.Add(link);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Task dependency created: Task {TaskId} -> {DependsOnId} ({Type})", taskId, dependsOnTaskId, dependencyType);
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
        try
        {
            await _projectAuth.EnsureCanEditAsync(projectId, operation: "RemoveTaskDependency", ct: ct);

            var tenantId = _tenantContext.TenantId ?? string.Empty;

            var link = await _context.TaskDependencies
                .AsTracking()
                .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.ProjectId == projectId && d.Id == dependencyId, ct);

            if (link == null) return Result<bool>.Failure("Dependency not found.");

            _context.TaskDependencies.Remove(link);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Task dependency {DependencyId} removed in project {ProjectId}", dependencyId, projectId);
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
                    Category = NotificationCategory.Personal,
                    Severity = task.Priority == TaskPriority.Urgent ? NotificationSeverity.Warning : NotificationSeverity.Info,
                    Title = title,
                    Message = message,
                    ProjectId = task.ProjectId,
                    UserId = targetUserId,
                    LinkUrl = linkUrl,
                    Channels = NotificationChannels.InApp | NotificationChannels.Email
                };

                await _notificationService.SendAsync(request, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to dispatch in-app notification to assignee {Email} for task {TaskId}", email, task.Id);
            }
        }
    }

    // --- SubTasks Operations ---

    public async Task<IReadOnlyList<SubTaskEntity>> GetSubTasksByTaskIdAsync(Guid taskId, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId ?? string.Empty;
        return await _context.SubTasks
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.ParentTaskId == taskId && !s.IsDeleted)
            .OrderBy(s => s.OrderIndex)
            .ThenBy(s => s.Created)
            .ToListAsync(ct);
    }

    public async Task<Result<SubTaskEntity>> CreateSubTaskAsync(SubTaskEntity subTask, CancellationToken ct = default)
    {
        try
        {
            var tenantId = _tenantContext.TenantId ?? string.Empty;
            var parentTask = await _context.Tasks.FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == subTask.ParentTaskId, ct);
            if (parentTask == null) return Result<SubTaskEntity>.Failure("Parent task does not exist.");

            if (parentTask.ProjectId != Guid.Empty)
            {
                await _projectAuth.EnsureCanEditAsync(parentTask.ProjectId, operation: "CreateSubTask", ct: ct);
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
            await _context.SaveChangesAsync(ct);
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
        try
        {
            var tenantId = _tenantContext.TenantId ?? string.Empty;
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

            await _context.SaveChangesAsync(ct);
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
        try
        {
            var tenantId = _tenantContext.TenantId ?? string.Empty;
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

            await _context.SaveChangesAsync(ct);
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete subtask {Id}", subTaskId);
            return Result<bool>.Failure($"Failed to delete subtask: {ex.Message}");
        }
    }

    // --- Blocker Dependency & Link Operations ---

    public async Task<IReadOnlyList<TaskLinkEntity>> GetTaskLinksAsync(Guid taskId, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId ?? string.Empty;
        return await _context.TaskLinks
            .AsNoTracking()
            .Include(l => l.SourceTask)
            .Include(l => l.TargetTask)
            .Include(l => l.BlockerDependency)
            .Where(l => l.TenantId == tenantId && (l.SourceTaskId == taskId || l.TargetTaskId == taskId) && !l.IsDeleted)
            .ToListAsync(ct);
    }

    public async Task<Result<TaskLinkEntity>> CreateBlockedByLinkAsync(
        Guid sourceTaskId,
        Guid targetTaskId,
        DateTime resolveDueDate,
        DependencyCriticality criticality,
        string impactDetails,
        string? watcherEmails = null,
        CancellationToken ct = default)
    {
        try
        {
            if (sourceTaskId == targetTaskId)
            {
                return Result<TaskLinkEntity>.Failure("A task cannot block itself.");
            }

            var tenantId = _tenantContext.TenantId ?? string.Empty;

            var sourceTask = await _context.Tasks.FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == sourceTaskId, ct);
            if (sourceTask == null) return Result<TaskLinkEntity>.Failure("Source task not found.");

            var targetTask = await _context.Tasks.FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == targetTaskId, ct);
            if (targetTask == null) return Result<TaskLinkEntity>.Failure("Target task (prerequisite) not found.");

            var existingLink = await _context.TaskLinks.FirstOrDefaultAsync(l =>
                l.TenantId == tenantId && l.SourceTaskId == sourceTaskId && l.TargetTaskId == targetTaskId && !l.IsDeleted, ct);

            if (existingLink != null)
            {
                return Result<TaskLinkEntity>.Failure("A link between these two tasks already exists.");
            }

            var blocker = new BlockerDependencyEntity
            {
                TenantId = tenantId,
                Level = sourceTask.Level,
                SourceTaskId = sourceTaskId,
                TargetTaskId = targetTaskId,
                Title = $"Blocker: '{sourceTask.Title}' blocked by '{targetTask.Title}'",
                Description = string.IsNullOrWhiteSpace(impactDetails)
                    ? $"Dependency contract. Prerequisite: '{targetTask.Title}'. Dependent: '{sourceTask.Title}'."
                    : impactDetails,
                Criticality = criticality,
                Status = targetTask.IsClosed ? TaskStatus.Closed : TaskStatus.Open,
                StartDate = DateTime.UtcNow,
                DueDate = resolveDueDate,
                EndDate = targetTask.IsClosed ? DateTime.UtcNow : null,
                ReporterEmail = _currentUser.Email ?? sourceTask.ReporterEmail,
                AssigneeEmails = targetTask.AssigneeEmails ?? targetTask.ReporterEmail,
                WatcherEmails = watcherEmails ?? string.Empty,
                ProgramId = sourceTask.ProgramId,
                ProjectId = sourceTask.ProjectId
            };

            await _context.BlockerDependencies.AddAsync(blocker, ct);

            var link = new TaskLinkEntity
            {
                TenantId = tenantId,
                SourceTaskId = sourceTaskId,
                TargetTaskId = targetTaskId,
                LinkType = TaskDependencyType.BlockedBy,
                BlockerDependency = blocker
            };

            await _context.TaskLinks.AddAsync(link, ct);
            await _context.SaveChangesAsync(ct);

            // Notify prerequisite assignee about the new blocker SLA
            var notifyEmails = blocker.GetParsedAssigneeEmails();
            foreach (var email in notifyEmails)
            {
                await SendBlockerAlertAsync(email, $"New Blocker Dependency Assigned: '{targetTask.Title}'",
                    $"Your task '{targetTask.Title}' is blocking '{sourceTask.Title}'. Resolve SLA: {resolveDueDate:yyyy-MM-dd}. Criticality: {criticality}.",
                    NotificationSeverity.Warning, targetTask.ProjectId, ct);
            }

            return Result<TaskLinkEntity>.Success(link);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create blocked-by link from {Source} to {Target}", sourceTaskId, targetTaskId);
            return Result<TaskLinkEntity>.Failure($"Failed to create blocker link: {ex.Message}");
        }
    }

    public async Task<Result<bool>> RemoveTaskLinkAsync(Guid linkId, CancellationToken ct = default)
    {
        try
        {
            var tenantId = _tenantContext.TenantId ?? string.Empty;
            var link = await _context.TaskLinks.AsTracking()
                .Include(l => l.BlockerDependency)
                .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.Id == linkId, ct);

            if (link == null) return Result<bool>.Failure("Link not found.");

            link.IsDeleted = true;
            link.DeletedAtUtc = DateTime.UtcNow;
            link.DeletedBy = _currentUser.Email ?? _currentUser.UserId;

            if (link.BlockerDependency != null)
            {
                link.BlockerDependency.IsDeleted = true;
                link.BlockerDependency.DeletedAtUtc = DateTime.UtcNow;
                link.BlockerDependency.DeletedBy = _currentUser.Email ?? _currentUser.UserId;
            }

            await _context.SaveChangesAsync(ct);
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove link {LinkId}", linkId);
            return Result<bool>.Failure($"Failed to remove link: {ex.Message}");
        }
    }

    private async Task SendBlockerAlertAsync(string email, string title, string message, NotificationSeverity severity, Guid projectId, CancellationToken ct)
    {
        try
        {
            var norm = email.Trim().ToLowerInvariant();
            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email.ToLower() == norm, ct);
            var userId = user != null ? user.Id.ToString() : email;

            var request = new SendNotificationReqDto
            {
                Category = NotificationCategory.Personal,
                Severity = severity,
                Title = title,
                Message = message,
                ProjectId = projectId,
                UserId = userId,
                Channels = NotificationChannels.InApp | NotificationChannels.Email
            };

            await _notificationService.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send blocker alert to {Email}", email);
        }
    }
}
