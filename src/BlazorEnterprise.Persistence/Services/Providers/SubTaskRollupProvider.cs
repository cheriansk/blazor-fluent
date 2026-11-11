using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Shared.DataListTypes;
using BlazorEnterprise.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace BlazorEnterprise.Persistence.Services.Providers;

public class SubTaskRollupProvider : IWorkItemRollupProvider
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public SubTaskRollupProvider(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public TaskItemTypeEnum ItemType => TaskItemTypeEnum.SubTask;

    public async Task<IReadOnlyList<CalendarItemDto>> GetCalendarRollupItemsAsync(
        Guid tenantId,
        TaskLevelEnum level,
        Guid? scopeId,
        string? userId,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken ct = default)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        var query = _context.SubTasks
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted && s.DueDate >= startUtc && s.DueDate <= endUtc);

        if (level == TaskLevelEnum.Program && scopeId.HasValue)
        {
            query = query.Where(s => s.ProgramId == scopeId.Value ||
                (s.ProjectId.HasValue && _context.Projects.Any(p => p.Id == s.ProjectId.Value && p.ProgramId == scopeId.Value)));
        }
        else if (level == TaskLevelEnum.Project && scopeId.HasValue)
        {
            query = query.Where(s => s.ProjectId == scopeId.Value);
        }
        else if (level == TaskLevelEnum.User && !string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(s => (s.AssigneeEmails != null && s.AssigneeEmails.Contains(userId)) || (s.WatcherEmails != null && s.WatcherEmails.Contains(userId)));
        }

        var items = await query.ToListAsync(ct);

        return items.Select(s => new CalendarItemDto
        {
            Id = s.Id,
            Title = s.Title,
            Description = s.Description ?? string.Empty,
            StartUtc = s.StartDate,
            EndUtc = s.DueDate,
            IsAllDay = true,
            ItemCategory = "SubTask",
            PriorityOrCriticality = s.Priority.ToString(),
            Status = s.Status.ToString(),
            OwnerOrAssignee = s.AssigneeEmails ?? s.ReporterEmail,
            ColorHex = "#5c2d91",
            BadgeIcon = "SubTask",
            SourceTaskId = s.ParentTaskId,
            ScopeId = s.ProjectId ?? s.ProgramId,
            Level = s.Level
        }).ToList();
    }
}
