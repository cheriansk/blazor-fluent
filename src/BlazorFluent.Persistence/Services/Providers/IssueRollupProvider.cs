using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace BlazorFluent.Persistence.Services.Providers;

public class IssueRollupProvider : IWorkItemRollupProvider
{
    private readonly AppDbContext _context;

    public IssueRollupProvider(AppDbContext context)
    {
        _context = context;
    }

    public TaskItemType ItemType => TaskItemType.Issue;

    public async Task<IReadOnlyList<CalendarItemDto>> GetCalendarRollupItemsAsync(
        string tenantId,
        TaskLevel level,
        Guid? scopeId,
        string? userId,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken ct = default)
    {
        var query = _context.Issues
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && !i.IsDeleted && i.DueDate >= startUtc && i.DueDate <= endUtc);

        if (level == TaskLevel.Program && scopeId.HasValue)
        {
            query = query.Where(i => i.ProgramId == scopeId.Value);
        }
        else if (level == TaskLevel.Project && scopeId.HasValue)
        {
            query = query.Where(i => i.ProjectId == scopeId.Value);
        }
        else if (level == TaskLevel.User && !string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(i => i.UserId == userId || i.AssigneeEmails.Contains(userId) || i.WatcherEmails.Contains(userId));
        }

        var items = await query.ToListAsync(ct);

        return items.Select(i => new CalendarItemDto
        {
            Id = i.Id,
            Title = $"[Issue] {i.Title}",
            Description = i.Description ?? string.Empty,
            StartUtc = i.StartDate,
            EndUtc = i.DueDate,
            IsAllDay = true,
            ItemCategory = "Issue",
            PriorityOrCriticality = $"Severity: {i.Severity}",
            Status = i.Status.ToString(),
            OwnerOrAssignee = i.AssigneeEmails ?? i.ReporterEmail,
            ColorHex = "#a80000",
            BadgeIcon = "ErrorBadge",
            ScopeId = i.ProjectId ?? i.ProgramId,
            Level = i.Level
        }).ToList();
    }
}
