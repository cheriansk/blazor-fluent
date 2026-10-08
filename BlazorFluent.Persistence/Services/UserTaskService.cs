using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Tasks;
using BlazorFluent.Core.Dtos;
using BlazorFluent.Core.Dtos.Response;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Persistence.Services;

public class UserTaskService : IUserTaskService
{
    private readonly AppDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantContext _tenantContext;
    private readonly INotificationService _notificationService;
    private readonly IProjectAuthorizationService _projectAuth;
    private readonly ILogger<UserTaskService> _logger;

    public UserTaskService(
        AppDbContext context,
        ICurrentUser currentUser,
        ITenantContext tenantContext,
        INotificationService notificationService,
        IProjectAuthorizationService projectAuth,
        ILogger<UserTaskService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
        _notificationService = notificationService;
        _projectAuth = projectAuth;
        _logger = logger;
    }

    public async Task<IReadOnlyList<UserTaskEntity>> GetTasksByProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        await _projectAuth.EnsureCanVisitAsync(projectId, operation: "GetTasksByProject", ct: ct);

        var tenantId = _tenantContext.TenantId ?? string.Empty;

        return await _context.Tasks
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.ProjectId == projectId)
            .Include(t => t.Comments.Where(c => !c.IsDeleted && c.TenantId == tenantId && c.ProjectId == projectId))
            .OrderBy(t => t.DueDate)
            .ToListAsync(ct);
    }

    public async Task<UserTaskEntity?> GetTaskByIdAsync(Guid projectId, Guid taskId, CancellationToken ct = default)
    {
        await _projectAuth.EnsureCanVisitAsync(projectId, operation: "GetTaskById", ct: ct);

        var tenantId = _tenantContext.TenantId ?? string.Empty;

        var task = await _context.Tasks
            .Include(t => t.Comments.Where(c => !c.IsDeleted && c.TenantId == tenantId && c.ProjectId == projectId).OrderBy(c => c.CreatedAtUtc))
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.ProjectId == projectId && t.Id == taskId, ct);

        return task;
    }

    public async Task<Result<UserTaskEntity>> CreateTaskAsync(UserTaskEntity task, CancellationToken ct = default)
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
                task.ReporterEmail = !string.IsNullOrWhiteSpace(_currentUser.Email)
                    ? _currentUser.Email
                    : (!string.IsNullOrWhiteSpace(_currentUser.UserName) ? _currentUser.UserName : "system");
            }

            _context.Tasks.Add(task);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Task '{Title}' ({Id}) created in project {ProjectId}", task.Title, task.Id, task.ProjectId);

            // Automated Notification: Alert all assignees
            await NotifyAssigneesAsync(task, isNew: true, ct);

            return Result<UserTaskEntity>.Success(task);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create task '{Title}'", task.Title);
            return Result<UserTaskEntity>.Failure($"Failed to create task: {ex.Message}");
        }
    }

    public async Task<Result<UserTaskEntity>> UpdateTaskAsync(UserTaskEntity updated, CancellationToken ct = default)
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
                return Result<UserTaskEntity>.Failure("Task not found in the specified project.");
            }

            // 2. Strict Project Immobility: Tasks can NEVER be reassigned or moved across projects
            if (updated.ProjectId != Guid.Empty && updated.ProjectId != task.ProjectId)
            {
                _logger.LogWarning("Security Violation: Attempt to move task {TaskId} from project {OldProject} to {NewProject}",
                    task.Id, task.ProjectId, updated.ProjectId);
                return Result<UserTaskEntity>.Failure("Cross-project task reassignment is forbidden. Tasks cannot be moved between projects.");
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
            return Result<UserTaskEntity>.Success(task);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update task {Id}", updated.Id);
            return Result<UserTaskEntity>.Failure($"Failed to update task: {ex.Message}");
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

            task.Status = UserTaskStatus.Closed;

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

            task.Status = UserTaskStatus.Open;

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

            task.Status = UserTaskStatus.Cancelled;

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

            var comment = new UserTaskCommentEntity
            {
                TaskId = taskId,
                ProjectId = projectId,
                TenantId = task.TenantId,
                AuthorUserId = _currentUser.UserId ?? "system",
                AuthorName = !string.IsNullOrWhiteSpace(_currentUser.UserName) ? _currentUser.UserName : (_currentUser.Email ?? "Anonymous"),
                AuthorEmail = _currentUser.Email ?? string.Empty,
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
                DependsOnTaskStatus = targetTask?.Status ?? UserTaskStatus.Open,
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

    private async Task NotifyAssigneesAsync(UserTaskEntity task, bool isNew, CancellationToken ct)
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
                    Severity = task.Priority == UserTaskPriority.Urgent ? NotificationSeverity.Warning : NotificationSeverity.Info,
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
}
