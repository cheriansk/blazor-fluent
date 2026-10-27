using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Shared.DataListTypes;
using BlazorEnterprise.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace BlazorEnterprise.Persistence.Services.Providers;

public class TaskRollupProvider : IWorkItemRollupProvider
{
    private readonly AppDbContext _context;

    public TaskRollupProvider(AppDbContext context)
    {
        _context = context;
    }

    public TaskItemTypeEnum ItemType => TaskItemTypeEnum.Task;

    public async Task<IReadOnlyList<CalendarItemDto>> GetCalendarRollupItemsAsync(
        Guid tenantId,
        TaskLevelEnum level,
        Guid? scopeId,
        string? userId,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken ct = default)
    {
        var query = _context.Tasks
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted && t.DueDate >= startUtc && t.DueDate <= endUtc);

        if (level == TaskLevelEnum.Program && scopeId.HasValue)
        {
            query = query.Where(t => t.ProgramId == scopeId.Value ||
                (t.ProjectId != Guid.Empty && _context.Projects.Any(p => p.Id == t.ProjectId && p.ProgramId == scopeId.Value)));
        }
        else if (level == TaskLevelEnum.Project && scopeId.HasValue)
        {
            query = query.Where(t => t.ProjectId == scopeId.Value);
        }
        else if (level == TaskLevelEnum.User && !string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(t => t.UserId == userId || (t.AssigneeEmails != null && t.AssigneeEmails.Contains(userId)) || (t.WatcherEmails != null && t.WatcherEmails.Contains(userId)));
        }

        var items = await query.ToListAsync(ct);

        return items.Select(t => new CalendarItemDto
        {
            Id = t.Id,
            Title = t.Title,
            Description = t.Description ?? string.Empty,
            StartUtc = t.StartDate,
            EndUtc = t.DueDate,
            IsAllDay = true,
            ItemCategory = "Task",
            PriorityOrCriticality = t.Priority.ToString(),
            Status = t.Status.ToString(),
            OwnerOrAssignee = t.AssigneeEmails ?? t.ReporterEmail,
            ColorHex = "#2b579a",
            BadgeIcon = "Task",
            SourceTaskId = t.Id,
            ScopeId = t.ProjectId != Guid.Empty ? t.ProjectId : t.ProgramId,
            Level = t.Level
        }).ToList();
    }
}
