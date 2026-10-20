using BlazorFluent.Core.Contracts;
using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace BlazorFluent.Persistence.Services.Providers;

public class BlockerRollupProvider : IWorkItemRollupProvider
{
    private readonly AppDbContext _context;

    public BlockerRollupProvider(AppDbContext context)
    {
        _context = context;
    }

    public TaskItemTypeEnum ItemType => TaskItemTypeEnum.BlockerDependency;

    public async Task<IReadOnlyList<CalendarItemDto>> GetCalendarRollupItemsAsync(
        string tenantId,
        TaskLevelEnum level,
        Guid? scopeId,
        string? userId,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken ct = default)
    {
        var query = _context.WorkDependencies
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId && !b.IsDeleted && b.DueDate >= startUtc && b.DueDate <= endUtc);

        if (level == TaskLevelEnum.Program && scopeId.HasValue)
        {
            query = query.Where(b => b.ProgramId == scopeId.Value ||
                b.DependsOnProgramId == scopeId.Value ||
                (b.ProjectId.HasValue && _context.Projects.Any(p => p.Id == b.ProjectId.Value && p.ProgramId == scopeId.Value)));
        }
        else if (level == TaskLevelEnum.Project && scopeId.HasValue)
        {
            query = query.Where(b => b.ProjectId == scopeId.Value || b.DependsOnProjectId == scopeId.Value);
        }
        else if (level == TaskLevelEnum.User && !string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(b => b.UserId == userId || (b.AssigneeEmails != null && b.AssigneeEmails.Contains(userId)) || (b.WatcherEmails != null && b.WatcherEmails.Contains(userId)));
        }

        var items = await query.ToListAsync(ct);

        return items.Select(b => new CalendarItemDto
        {
            Id = b.Id,
            Title = $"[Blocker] {b.Title}",
            Description = $"{b.ImpactDetails}\n(Waiting on: {b.DependsOnSnapshotTitle ?? "Prerequisite"})",
            StartUtc = b.StartDate,
            EndUtc = b.DueDate,
            IsAllDay = true,
            ItemCategory = "Blocker",
            PriorityOrCriticality = $"Criticality: {b.Criticality}",
            Status = b.Status.ToString(),
            OwnerOrAssignee = b.AssigneeEmails ?? b.ReporterEmail ?? string.Empty,
            ColorHex = b.IsScheduleConflict ? "#d13438" : "#e81123",
            BadgeIcon = "Blocked",
            SourceTaskId = b.DependentItemId,
            ScopeId = b.ProjectId ?? b.ProgramId,
            Level = b.Level
        }).ToList();
    }
}
