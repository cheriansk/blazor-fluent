using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace BlazorFluent.Persistence.Services.Providers;

public class AssumptionRollupProvider : IWorkItemRollupProvider
{
    private readonly AppDbContext _context;

    public AssumptionRollupProvider(AppDbContext context)
    {
        _context = context;
    }

    public TaskItemType ItemType => TaskItemType.Assumption;

    public async Task<IReadOnlyList<CalendarItemDto>> GetCalendarRollupItemsAsync(
        string tenantId,
        TaskLevel level,
        Guid? scopeId,
        string? userId,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken ct = default)
    {
        var query = _context.Assumptions
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && !a.IsDeleted && a.DueDate >= startUtc && a.DueDate <= endUtc);

        if (level == TaskLevel.Program && scopeId.HasValue)
        {
            query = query.Where(a => a.ProgramId == scopeId.Value);
        }
        else if (level == TaskLevel.Project && scopeId.HasValue)
        {
            query = query.Where(a => a.ProjectId == scopeId.Value);
        }
        else if (level == TaskLevel.User && !string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(a => a.UserId == userId || a.AssigneeEmails.Contains(userId) || a.WatcherEmails.Contains(userId));
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
