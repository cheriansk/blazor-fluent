using BlazorFluent.Core.Contracts;
using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace BlazorFluent.Persistence.Services.Providers;

public class RiskRollupProvider : IWorkItemRollupProvider
{
    private readonly AppDbContext _context;

    public RiskRollupProvider(AppDbContext context)
    {
        _context = context;
    }

    public TaskItemTypeEnum ItemType => TaskItemTypeEnum.Risk;

    public async Task<IReadOnlyList<CalendarItemDto>> GetCalendarRollupItemsAsync(
        Guid tenantId,
        TaskLevelEnum level,
        Guid? scopeId,
        string? userId,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken ct = default)
    {
        var query = _context.Risks
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && !r.IsDeleted && r.DueDate >= startUtc && r.DueDate <= endUtc);

        if (level == TaskLevelEnum.Tenant)
        {
            query = query.Where(r => r.Level == TaskLevelEnum.Tenant);
        }
        else if (level == TaskLevelEnum.Program && scopeId.HasValue)
        {
            query = query.Where(r => r.ProgramId == scopeId.Value);
        }
        else if (level == TaskLevelEnum.Project && scopeId.HasValue)
        {
            query = query.Where(r => r.ProjectId == scopeId.Value);
        }
        else if (level == TaskLevelEnum.User && !string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(r => r.UserId == userId || r.AssigneeEmails.Contains(userId) || r.WatcherEmails.Contains(userId));
        }

        var items = await query.ToListAsync(ct);

        return items.Select(r => new CalendarItemDto
        {
            Id = r.Id,
            Title = $"[Risk] {r.Title}",
            Description = r.Description ?? string.Empty,
            StartUtc = r.StartDate,
            EndUtc = r.DueDate,
            IsAllDay = true,
            ItemCategory = "Risk",
            PriorityOrCriticality = $"Impact: {r.Impact}, Prob: {r.Probability}",
            Status = r.RiskStatus.ToString(),
            OwnerOrAssignee = r.AssigneeEmails ?? r.ReporterEmail,
            ColorHex = "#d83b01",
            BadgeIcon = "Warning",
            ScopeId = r.ProjectId ?? r.ProgramId,
            Level = r.Level
        }).ToList();
    }
}
