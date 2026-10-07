using System.Text;
using BlazorFluent.Core.Common;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Tasks;
using BlazorFluent.Jobs.Abstractions;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Jobs.Jobs.Tasks;

/// <summary>
/// Enterprise delivery cadence tracking engine.
/// Evaluates approaching deadlines (T-5, T-3, T-1, T-0), daily overdue escalation,
/// and blocker dependency resolution requirements across all tenants and projects.
/// Dispatches smart daily digests for upcoming notices and immediate alerts for overdue/blockers.
/// </summary>
public class TaskCadenceAlertJobHandler : IBatchJobHandler<TaskCadenceAlertJobEvent>
{
    private readonly AppDbContext _dbContext;
    private readonly INotificationService _notificationService;
    private readonly ILogger<TaskCadenceAlertJobHandler> _logger;

    public TaskCadenceAlertJobHandler(
        AppDbContext dbContext,
        INotificationService notificationService,
        ILogger<TaskCadenceAlertJobHandler> logger)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task HandleAsync(TaskCadenceAlertJobEvent jobEvent, CancellationToken cancellationToken)
    {
        _logger.LogInformation("TaskCadenceAlertJobHandler initiated cadence deadline evaluation.");

        var today = DateTime.UtcNow.Date;

        // 1. Fetch active tasks across all tenants
        var activeTasks = await _dbContext.Tasks
            .IgnoreQueryFilters()
            .Where(t => t.Status != UserTaskStatus.Closed && t.Status != UserTaskStatus.Cancelled)
            .OrderBy(t => t.DueDate)
            .ToListAsync(cancellationToken);

        if (activeTasks.Count == 0)
        {
            _logger.LogInformation("No active tasks found for cadence notification.");
            return;
        }

        var taskLookup = activeTasks.ToDictionary(t => t.Id);

        // 2. Fetch blocking dependencies
        var blockingDependencies = await _dbContext.TaskDependencies
            .IgnoreQueryFilters()
            .Where(d => d.DependencyType == TaskDependencyType.Blocks)
            .ToListAsync(cancellationToken);

        // 3. User email to ID lookup for in-app notification routing
        var allUserEmails = activeTasks
            .SelectMany(t => t.GetParsedAssigneeEmails())
            .Select(e => e.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        var usersByEmail = await _dbContext.Users
            .IgnoreQueryFilters()
            .Where(u => allUserEmails.Contains(u.Email.ToLower()))
            .ToDictionaryAsync(u => u.Email.ToLower(), u => u.Id.ToString(), cancellationToken);

        // Data structures for grouping alerts per assignee
        var overdueByAssignee = new Dictionary<string, List<UserTaskEntity>>();
        var dueTodayByAssignee = new Dictionary<string, List<UserTaskEntity>>();
        var digestUpcomingByAssignee = new Dictionary<string, List<(UserTaskEntity Task, int DaysRemaining)>>();
        var blockerAlertsByAssignee = new Dictionary<string, List<(UserTaskEntity BlockerTask, UserTaskEntity BlockedTask, DateTime? ResolveBy)>>();

        // 4. Evaluate task deadlines
        foreach (var task in activeTasks)
        {
            var assignees = task.GetParsedAssigneeEmails();
            if (assignees.Count == 0) continue;

            var days = (task.DueDate.Date - today).Days;

            foreach (var email in assignees)
            {
                var normEmail = email.Trim().ToLowerInvariant();

                if (days < 0)
                {
                    // Past due: everyday overdue notification
                    if (!overdueByAssignee.ContainsKey(normEmail)) overdueByAssignee[normEmail] = new();
                    overdueByAssignee[normEmail].Add(task);
                }
                else if (days == 0)
                {
                    // Due today (T-0)
                    if (!dueTodayByAssignee.ContainsKey(normEmail)) dueTodayByAssignee[normEmail] = new();
                    dueTodayByAssignee[normEmail].Add(task);
                }
                else if (days is 1 or 3 or 5)
                {
                    // T-1, T-3, T-5 approaching cadence
                    if (!digestUpcomingByAssignee.ContainsKey(normEmail)) digestUpcomingByAssignee[normEmail] = new();
                    digestUpcomingByAssignee[normEmail].Add((task, days));
                }
            }
        }

        // 5. Evaluate blocking dependencies
        foreach (var dep in blockingDependencies)
        {
            if (!taskLookup.TryGetValue(dep.DependsOnTaskId, out var blockerTask) ||
                !taskLookup.TryGetValue(dep.TaskId, out var blockedTask))
            {
                continue; // Either blocker or blocked task is already closed/cancelled or not found
            }

            var blockerAssignees = blockerTask.GetParsedAssigneeEmails();
            var targetDueDays = (blockedTask.DueDate.Date - today).Days;

            // Check if blocker deadline is approaching or breached
            var isDepOverdue = dep.ResolveByUtc.HasValue && dep.ResolveByUtc.Value.Date < today;
            var isDepDueToday = dep.ResolveByUtc.HasValue && dep.ResolveByUtc.Value.Date == today;
            var isTargetUrgent = targetDueDays <= 3; // Blocked task is due soon!

            if (isDepOverdue || isDepDueToday || isTargetUrgent)
            {
                foreach (var email in blockerAssignees)
                {
                    var normEmail = email.Trim().ToLowerInvariant();
                    if (!blockerAlertsByAssignee.ContainsKey(normEmail)) blockerAlertsByAssignee[normEmail] = new();
                    blockerAlertsByAssignee[normEmail].Add((blockerTask, blockedTask, dep.ResolveByUtc));
                }
            }
        }

        var alertsDispatched = 0;

        // 6. Dispatch Overdue Alerts (High Priority)
        foreach (var (email, tasks) in overdueByAssignee)
        {
            var userId = usersByEmail.TryGetValue(email, out var uid) ? uid : email;
            var firstProject = tasks[0].ProjectId;

            var message = tasks.Count == 1
                ? $"⚠️ Task '{tasks[0].Title}' is past its due date ({tasks[0].DueDate:yyyy-MM-dd}). Please update or close it."
                : $"⚠️ You have {tasks.Count} overdue tasks past their deadlines: {string.Join(", ", tasks.Take(3).Select(t => $"'{t.Title}'"))}{(tasks.Count > 3 ? "..." : "")}.";

            await _notificationService.SendAsync(new SendNotificationRequest
            {
                Category = NotificationCategory.Personal,
                Severity = NotificationSeverity.Error,
                Title = $"🚨 Overdue Task Alert ({tasks.Count} item{(tasks.Count > 1 ? "s" : "")})",
                Message = message,
                ProjectId = firstProject,
                UserId = userId,
                LinkUrl = $"/projects/{firstProject}/tasks",
                Channels = NotificationChannels.InApp | NotificationChannels.Email
            }, cancellationToken);

            alertsDispatched++;
        }

        // 7. Dispatch Due Today Alerts (Warning Priority)
        foreach (var (email, tasks) in dueTodayByAssignee)
        {
            var userId = usersByEmail.TryGetValue(email, out var uid) ? uid : email;
            var firstProject = tasks[0].ProjectId;

            var message = tasks.Count == 1
                ? $"⏰ Task '{tasks[0].Title}' is due today ({today:yyyy-MM-dd})."
                : $"⏰ You have {tasks.Count} tasks due today: {string.Join(", ", tasks.Take(3).Select(t => $"'{t.Title}'"))}{(tasks.Count > 3 ? "..." : "")}.";

            await _notificationService.SendAsync(new SendNotificationRequest
            {
                Category = NotificationCategory.Personal,
                Severity = NotificationSeverity.Warning,
                Title = $"⏰ Tasks Due Today ({tasks.Count})",
                Message = message,
                ProjectId = firstProject,
                UserId = userId,
                LinkUrl = $"/projects/{firstProject}/tasks",
                Channels = NotificationChannels.InApp | NotificationChannels.Email
            }, cancellationToken);

            alertsDispatched++;
        }

        // 8. Dispatch Smart Daily Morning Digest for T-5, T-3, T-1 upcoming notices
        foreach (var (email, items) in digestUpcomingByAssignee)
        {
            var userId = usersByEmail.TryGetValue(email, out var uid) ? uid : email;
            var firstProject = items[0].Task.ProjectId;

            var sb = new StringBuilder();
            sb.AppendLine("Upcoming deadline notices:");
            foreach (var item in items.OrderBy(i => i.DaysRemaining))
            {
                var cadenceLabel = item.DaysRemaining == 1 ? "Due Tomorrow" : $"Due in {item.DaysRemaining} days";
                sb.AppendLine($"• [{cadenceLabel}] {item.Task.Title} (Due {item.Task.DueDate:yyyy-MM-dd})");
            }

            await _notificationService.SendAsync(new SendNotificationRequest
            {
                Category = NotificationCategory.Personal,
                Severity = NotificationSeverity.Info,
                Title = $"📅 Upcoming Deadlines ({items.Count} items)",
                Message = sb.ToString(),
                ProjectId = firstProject,
                UserId = userId,
                LinkUrl = $"/projects/{firstProject}/tasks",
                Channels = NotificationChannels.InApp | NotificationChannels.Email
            }, cancellationToken);

            alertsDispatched++;
        }

        // 9. Dispatch Blocker Escalation Alerts
        foreach (var (email, items) in blockerAlertsByAssignee)
        {
            var userId = usersByEmail.TryGetValue(email, out var uid) ? uid : email;
            var firstProject = items[0].BlockerTask.ProjectId;

            var sb = new StringBuilder();
            sb.AppendLine("You are assigned to tasks blocking other deliverables:");
            foreach (var (blocker, blocked, resolveBy) in items)
            {
                var resolveByText = resolveBy.HasValue ? $" (Resolve by {resolveBy.Value:yyyy-MM-dd})" : "";
                sb.AppendLine($"• Your task '{blocker.Title}' blocks '{blocked.Title}'{resolveByText}");
            }

            await _notificationService.SendAsync(new SendNotificationRequest
            {
                Category = NotificationCategory.Personal,
                Severity = NotificationSeverity.Warning,
                Title = $"⛔ Blocker Resolution Alert ({items.Count} blocking item{(items.Count > 1 ? "s" : "")})",
                Message = sb.ToString(),
                ProjectId = firstProject,
                UserId = userId,
                LinkUrl = $"/projects/{firstProject}/tasks",
                Channels = NotificationChannels.InApp | NotificationChannels.Email
            }, cancellationToken);

            alertsDispatched++;
        }

        _logger.LogInformation("TaskCadenceAlertJobHandler completed. Dispatched {Count} cadence notifications.", alertsDispatched);
    }
}
