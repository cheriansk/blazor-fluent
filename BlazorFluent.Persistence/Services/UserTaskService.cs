using BlazorFluent.Core.Common;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Tasks;
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
    private readonly ILogger<UserTaskService> _logger;

    public UserTaskService(
        AppDbContext context,
        ICurrentUser currentUser,
        ITenantContext tenantContext,
        INotificationService notificationService,
        ILogger<UserTaskService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<UserTaskEntity>> GetTasksByProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        return await _context.Tasks
            .AsNoTracking()
            .Where(t => t.ProjectId == projectId)
            .Include(t => t.Comments.Where(c => !c.IsDeleted))
            .OrderBy(t => t.DueDate)
            .ToListAsync(ct);
    }

    public async Task<UserTaskEntity?> GetTaskByIdAsync(Guid taskId, CancellationToken ct = default)
    {
        return await _context.Tasks
            .Include(t => t.Comments.Where(c => !c.IsDeleted).OrderBy(c => c.CreatedAtUtc))
            .FirstOrDefaultAsync(t => t.Id == taskId, ct);
    }

    public async Task<Result<UserTaskEntity>> CreateTaskAsync(UserTaskEntity task, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(task.TenantId))
            {
                task.TenantId = _tenantContext.TenantId ?? string.Empty;
            }

            if (task.Status == UserTaskStatus.Closed)
            {
                task.IsClosed = true;
                task.ClosedAtUtc = DateTime.UtcNow;
                task.ClosedBy = _currentUser.UserId;
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
            var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == updated.Id, ct);
            if (task is null)
            {
                return Result<UserTaskEntity>.Failure("Task not found.");
            }

            var wasClosed = task.IsClosed;

            task.Title = updated.Title;
            task.Description = updated.Description;
            task.Priority = updated.Priority;
            task.Status = updated.Status;
            task.DueDate = updated.DueDate;
            task.AssigneeEmails = updated.AssigneeEmails;
            task.Labels = updated.Labels;

            if (updated.Status == UserTaskStatus.Closed && !wasClosed)
            {
                task.IsClosed = true;
                task.ClosedAtUtc = DateTime.UtcNow;
                task.ClosedBy = _currentUser.UserId;
            }
            else if (updated.Status != UserTaskStatus.Closed && wasClosed)
            {
                task.IsClosed = false;
                task.ClosedAtUtc = null;
                task.ClosedBy = null;
            }

            await _context.SaveChangesAsync(ct);
            return Result<UserTaskEntity>.Success(task);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update task {Id}", updated.Id);
            return Result<UserTaskEntity>.Failure($"Failed to update task: {ex.Message}");
        }
    }

    public async Task<Result<bool>> CloseTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        try
        {
            var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == taskId, ct);
            if (task is null) return Result<bool>.Failure("Task not found.");

            task.Status = UserTaskStatus.Closed;
            task.IsClosed = true;
            task.ClosedAtUtc = DateTime.UtcNow;
            task.ClosedBy = _currentUser.UserId;

            await _context.SaveChangesAsync(ct);
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to close task {Id}", taskId);
            return Result<bool>.Failure($"Failed to close task: {ex.Message}");
        }
    }

    public async Task<Result<bool>> ReopenTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        try
        {
            var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == taskId, ct);
            if (task is null) return Result<bool>.Failure("Task not found.");

            task.Status = UserTaskStatus.Open;
            task.IsClosed = false;
            task.ClosedAtUtc = null;
            task.ClosedBy = null;

            await _context.SaveChangesAsync(ct);
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reopen task {Id}", taskId);
            return Result<bool>.Failure($"Failed to reopen task: {ex.Message}");
        }
    }

    public async Task<Result<UserTaskCommentEntity>> AddCommentAsync(Guid taskId, string commentText, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(commentText))
            {
                return Result<UserTaskCommentEntity>.Failure("Comment text cannot be empty.");
            }

            var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == taskId, ct);
            if (task is null) return Result<UserTaskCommentEntity>.Failure("Task not found.");

            var comment = new UserTaskCommentEntity
            {
                TaskId = taskId,
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

    public async Task<IReadOnlyList<UserTaskCommentEntity>> GetCommentsAsync(Guid taskId, CancellationToken ct = default)
    {
        return await _context.TaskComments
            .AsNoTracking()
            .Where(c => c.TaskId == taskId && !c.IsDeleted)
            .OrderBy(c => c.CreatedAtUtc)
            .ToListAsync(ct);
    }

    private async Task NotifyAssigneesAsync(UserTaskEntity task, bool isNew, CancellationToken ct)
    {
        var emails = task.GetParsedAssigneeEmails();
        if (emails.Count == 0) return;

        var dueDateFormatted = task.DueDate.HasValue ? task.DueDate.Value.ToString("yyyy-MM-dd") : "No due date";
        var title = isNew ? $"New Task Assigned: {task.Title}" : $"Task Updated: {task.Title}";
        var message = $"You have been assigned to task '{task.Title}'. Priority: {task.Priority}, Due: {dueDateFormatted}.";
        var linkUrl = $"/projects/{task.ProjectId}/tasks";

        foreach (var email in emails)
        {
            try
            {
                // Find matching user in identity if available
                var targetUser = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower(), ct);

                var targetUserId = targetUser?.Id.ToString() ?? email;

                var request = new SendNotificationRequest
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
