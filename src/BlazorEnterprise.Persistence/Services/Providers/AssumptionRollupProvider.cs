using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Shared.DataListTypes;
using BlazorEnterprise.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace BlazorEnterprise.Persistence.Services.Providers;

public class AssumptionRollupProvider : IWorkItemRollupProvider
{
    private readonly AppDbContext _context;

    public AssumptionRollupProvider(AppDbContext context)
    {
        _context = context;
    }

    public TaskItemTypeEnum ItemType => TaskItemTypeEnum.Assumption;

    public async Task<IReadOnlyList<CalendarItemDto>> GetCalendarRollupItemsAsync(
        Guid tenantId,
        TaskLevelEnum level,
        Guid? scopeId,
        string? userId,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken ct = default)
    {
        var query = _context.Assumptions
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && !a.IsDeleted && a.DueDate >= startUtc && a.DueDate <= endUtc);

        if (level == TaskLevelEnum.Program && scopeId.HasValue)
        {
            query = query.Where(a => a.ProgramId == scopeId.Value);
        }
        else if (level == TaskLevelEnum.Project && scopeId.HasValue)
        {
            query = query.Where(a => a.ProjectId == scopeId.Value);
        }
        else if (level == TaskLevelEnum.User && !string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(a => a.UserId == userId || (a.AssigneeEmails != null && a.AssigneeEmails.Contains(userId)) || (a.WatcherEmails != null && a.WatcherEmails.Contains(userId)));
        }

        var items = await query.ToListAsync(ct);

        return items.Select(a => new CalendarItemDto
        {
            Id = a.Id,
            Title = $"[Assumption] {a.Title}",
            Description = a.Description ?? string.Empty,
            StartUtc = a.StartDate,
            EndUtc = a.DueDate,
            IsAllDay = true,
            ItemCategory = "Assumption",
            PriorityOrCriticality = a.Priority.ToString(),
            Status = a.ValidationStatus.ToString(),
            OwnerOrAssignee = a.AssigneeEmails ?? a.ReporterEmail,
            ColorHex = "#008272",
            BadgeIcon = "Lightbulb",
            ScopeId = a.ProjectId ?? a.ProgramId,
            Level = a.Level
        }).ToList();
    }
}
