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

    public TaskItemType ItemType => TaskItemType.BlockerDependency;

    public async Task<IReadOnlyList<CalendarItemDto>> GetCalendarRollupItemsAsync(
        string tenantId,
        TaskLevel level,
        Guid? scopeId,
        string? userId,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken ct = default)
    {
        var query = _context.BlockerDependencies
            .AsNoTracking()
            .Include(b => b.SourceTask)
            .Include(b => b.TargetTask)
            .Where(b => b.TenantId == tenantId && !b.IsDeleted && b.DueDate >= startUtc && b.DueDate <= endUtc);

        if (level == TaskLevel.Program && scopeId.HasValue)
        {
            query = query.Where(b => b.ProgramId == scopeId.Value ||
                (b.ProjectId.HasValue && _context.Projects.Any(p => p.Id == b.ProjectId.Value && p.ProgramId == scopeId.Value)));
        }
        else if (level == TaskLevel.Project && scopeId.HasValue)
        {
            query = query.Where(b => b.ProjectId == scopeId.Value ||
                (b.SourceTask != null && b.SourceTask.ProjectId == scopeId.Value) ||
                (b.TargetTask != null && b.TargetTask.ProjectId == scopeId.Value));
        }
        else if (level == TaskLevel.User && !string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(b => b.UserId == userId || b.AssigneeEmails.Contains(userId) || b.WatcherEmails.Contains(userId));
        }

        var items = await query.ToListAsync(ct);

        return items.Select(b => new CalendarItemDto
        {
            Id = b.Id,
            Title = $"[Blocker] {b.Title}",
            Description = $"{b.ImpactDetails}\n(Blocks: {b.SourceTask?.Title ?? "Task"}, Blocked By: {b.TargetTask?.Title ?? "Task"})",
            StartUtc = b.StartDate,
            EndUtc = b.DueDate,
            IsAllDay = true,
            ItemCategory = "Blocker",
            PriorityOrCriticality = $"Criticality: {b.Criticality}",
            Status = b.Status.ToString(),
            OwnerOrAssignee = b.AssigneeEmails ?? b.ReporterEmail,
            ColorHex = "#e81123",
            BadgeIcon = "Blocked",
            SourceTaskId = b.SourceTaskId,
            ScopeId = b.ProjectId ?? b.ProgramId,
            Level = b.Level
        }).ToList();
    }
}
