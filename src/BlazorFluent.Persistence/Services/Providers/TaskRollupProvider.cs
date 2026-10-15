using BlazorFluent.Core.Contracts;
using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace BlazorFluent.Persistence.Services.Providers;

public class TaskRollupProvider : IWorkItemRollupProvider
{
    private readonly AppDbContext _context;

    public TaskRollupProvider(AppDbContext context)
    {
        _context = context;
    }

    public TaskItemType ItemType => TaskItemType.Task;

    public async Task<IReadOnlyList<CalendarItemDto>> GetCalendarRollupItemsAsync(
        string tenantId,
        TaskLevel level,
        Guid? scopeId,
        string? userId,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken ct = default)
    {
        var query = _context.Tasks
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted && t.DueDate >= startUtc && t.DueDate <= endUtc);

        if (level == TaskLevel.Program && scopeId.HasValue)
        {
            query = query.Where(t => t.ProgramId == scopeId.Value ||
                (t.ProjectId != Guid.Empty && _context.Projects.Any(p => p.Id == t.ProjectId && p.ProgramId == scopeId.Value)));
        }
        else if (level == TaskLevel.Project && scopeId.HasValue)
        {
            query = query.Where(t => t.ProjectId == scopeId.Value);
        }
        else if (level == TaskLevel.User && !string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(t => t.UserId == userId || t.AssigneeEmails.Contains(userId) || t.WatcherEmails.Contains(userId));
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
