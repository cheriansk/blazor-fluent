using System.Text;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Notifications;
using BlazorFluent.Jobs.Abstractions;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Jobs.Jobs.Tasks;

/// <summary>
/// Background batch handler: compiles and dispatches the end-of-day task summary table to Microsoft Teams.
/// Groups pending items into: Past Due, Due Today, Due Tomorrow, and Due in 2 Days.
/// Dispatches to the project's dedicated Teams webhook, falling back to the global configuration.
/// </summary>
public class DailyTaskSummaryJobHandler : IBatchJobHandler<DailyTaskSummaryJobEvent>
{
    private readonly AppDbContext _dbContext;
    private readonly ITeamsNotificationSender _teamsSender;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DailyTaskSummaryJobHandler> _logger;

    public DailyTaskSummaryJobHandler(
        AppDbContext dbContext,
        ITeamsNotificationSender teamsSender,
        IConfiguration configuration,
        ILogger<DailyTaskSummaryJobHandler> logger)
    {
        _dbContext = dbContext;
        _teamsSender = teamsSender;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task HandleAsync(DailyTaskSummaryJobEvent jobEvent, CancellationToken cancellationToken)
    {
        _logger.LogInformation("DailyTaskSummaryJobHandler starting end-of-day task summary dispatch.");

        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        var in2Days = today.AddDays(2);

        var globalWebhook = _configuration["Teams:DefaultWebhookUrl"];

        // Query active projects for the scoped tenant
        var projects = await _dbContext.Projects
            .Where(p => p.IsActive && !p.IsDeleted)
            .ToListAsync(cancellationToken);

        var dispatchedCount = 0;

        // Batch retrieve all actionable tasks across target projects in a single query
        var projectIds = projects.Select(p => p.Id).ToList();
        var allTasks = await _dbContext.Tasks
            .Where(t => projectIds.Contains(t.ProjectId) &&
                        t.Status != UserTaskStatus.Closed &&
                        t.Status != UserTaskStatus.Cancelled)
            .OrderBy(t => t.DueDate)
            .ToListAsync(cancellationToken);

        var tasksByProject = allTasks.ToLookup(t => t.ProjectId);

        foreach (var project in projects)
        {
            var webhookUrl = !string.IsNullOrWhiteSpace(project.TeamsWebhookUrl)
                ? project.TeamsWebhookUrl
                : globalWebhook;

            if (string.IsNullOrWhiteSpace(webhookUrl))
            {
                continue;
            }

            var tasks = tasksByProject[project.Id].ToList();

            var pastDue = tasks.Where(t => t.DueDate.Date < today).ToList();
            var dueToday = tasks.Where(t => t.DueDate.Date == today).ToList();
            var dueTomorrow = tasks.Where(t => t.DueDate.Date == tomorrow).ToList();
            var dueIn2Days = tasks.Where(t => t.DueDate.Date == in2Days).ToList();

            var totalPending = pastDue.Count + dueToday.Count + dueTomorrow.Count + dueIn2Days.Count;
            if (totalPending == 0)
            {
                continue;
            }

            // Build Markdown table
            var sb = new StringBuilder();
            sb.AppendLine($"### 📋 Task Summary: **{project.Name}**");
            sb.AppendLine($"**As of:** {today:yyyy-MM-dd} | **Actionable Tasks:** {totalPending}");
            sb.AppendLine();
            sb.AppendLine("| Status | Priority | Title | Due Date | Assignees |");
            sb.AppendLine("|---|---|---|---|---|");

            foreach (var t in pastDue)
            {
                sb.AppendLine($"| 🔴 **Past Due** | {t.Priority} | {Sanitize(t.Title)} | {t.DueDate:yyyy-MM-dd} | {Sanitize(t.AssigneeEmails)} |");
            }
            foreach (var t in dueToday)
            {
                sb.AppendLine($"| 🟠 **Due Today** | {t.Priority} | {Sanitize(t.Title)} | {t.DueDate:yyyy-MM-dd} | {Sanitize(t.AssigneeEmails)} |");
            }
            foreach (var t in dueTomorrow)
            {
                sb.AppendLine($"| 🔵 **Due Tomorrow** | {t.Priority} | {Sanitize(t.Title)} | {t.DueDate:yyyy-MM-dd} | {Sanitize(t.AssigneeEmails)} |");
            }
            foreach (var t in dueIn2Days)
            {
                sb.AppendLine($"| 🟣 **In 2 Days** | {t.Priority} | {Sanitize(t.Title)} | {t.DueDate:yyyy-MM-dd} | {Sanitize(t.AssigneeEmails)} |");
            }

            var notification = new NotificationEntity
            {
                TenantId = project.TenantId,
                ProjectId = project.Id,
                Title = $"Daily Task Digest: {project.Name}",
                Message = sb.ToString(),
                Severity = pastDue.Count > 0 ? NotificationSeverity.Warning : NotificationSeverity.Info,
                Category = NotificationCategory.Generic,
                LinkUrl = $"/projects/{project.Id}/tasks"
            };

            var sent = await _teamsSender.SendTeamsNotificationAsync(notification, webhookUrl, cancellationToken);
            if (sent)
            {
                dispatchedCount++;
                _logger.LogInformation("Dispatched daily task summary for project '{ProjectName}' ({Count} items).", project.Name, totalPending);
            }
        }

        _logger.LogInformation("DailyTaskSummaryJobHandler finished. Dispatched summaries for {Count} project(s).", dispatchedCount);
    }

    private static string Sanitize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "-";
        return text.Replace("|", "\\|").Trim();
    }
}
