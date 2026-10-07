using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Persistence.Services;

/// <summary>
/// Real-time health evaluation engine for projects and milestones.
/// Analyzes delivery cadence, overdue items, blocker bottlenecks, and predecessor delays
/// with transparent root-cause attribution.
/// </summary>
public class ProjectHealthService : IProjectHealthService
{
    private readonly AppDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly IProjectAuthorizationService _projectAuth;
    private readonly ILogger<ProjectHealthService> _logger;

    public ProjectHealthService(
        AppDbContext context,
        ITenantContext tenantContext,
        IProjectAuthorizationService projectAuth,
        ILogger<ProjectHealthService> logger)
    {
        _context = context;
        _tenantContext = tenantContext;
        _projectAuth = projectAuth;
        _logger = logger;
    }

    public async Task<ProjectHealthSummaryDto> GetProjectHealthAsync(Guid projectId, CancellationToken ct = default)
    {
        await _projectAuth.EnsureCanVisitAsync(projectId, operation: "GetProjectHealth", ct: ct);

        var tenantId = _tenantContext.TenantId ?? string.Empty;
        var today = DateTime.UtcNow.Date;

        var project = await _context.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == projectId, ct);

        var tasks = await _context.Tasks
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.ProjectId == projectId)
            .ToListAsync(ct);

        var milestones = await _context.Milestones
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.ProjectId == projectId)
            .ToListAsync(ct);

        var blockingDependencies = await _context.TaskDependencies
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.ProjectId == projectId && d.DependencyType == TaskDependencyType.Blocks)
            .ToListAsync(ct);

        // Map task statuses for fast blocker checking
        var taskStatusLookup = tasks.ToDictionary(t => t.Id, t => t.Status);

        var activeBlockersCount = 0;
        var overdueBlockersCount = 0;

        foreach (var dep in blockingDependencies)
        {
            if (taskStatusLookup.TryGetValue(dep.DependsOnTaskId, out var depStatus))
            {
                var isResolved = depStatus is UserTaskStatus.Closed or UserTaskStatus.Cancelled;
                if (!isResolved)
                {
                    activeBlockersCount++;
                    if (dep.ResolveByUtc.HasValue && dep.ResolveByUtc.Value.Date < today)
                    {
                        overdueBlockersCount++;
                    }
                }
            }
        }

        var totalTasks = tasks.Count(t => t.Status != UserTaskStatus.Cancelled);
        var completedTasks = tasks.Count(t => t.Status == UserTaskStatus.Closed);
        var inProgressTasks = tasks.Count(t => t.Status == UserTaskStatus.InProgress);
        var openTasks = tasks.Count(t => t.Status == UserTaskStatus.Open);
        var overdueTasks = tasks.Count(t => !t.IsClosed && t.DueDate.Date < today);

        var summary = new ProjectHealthSummaryDto
        {
            ProjectId = projectId,
            ProjectName = project?.Name ?? "Project",
            TotalTasks = totalTasks,
            CompletedTasks = completedTasks,
            InProgressTasks = inProgressTasks,
            OpenTasks = openTasks,
            OverdueTasks = overdueTasks,
            ActiveBlockersCount = activeBlockersCount,
            TotalMilestones = milestones.Count(m => m.Status != MilestoneStatus.Cancelled),
            CompletedMilestones = milestones.Count(m => m.Status == MilestoneStatus.Completed)
        };

        // Root cause attribution
        if (overdueBlockersCount > 0)
        {
            summary.RiskReasons.Add($"{overdueBlockersCount} blocking task dependency deadline(s) have breached without resolution.");
        }
        else if (activeBlockersCount > 0)
        {
            summary.RiskReasons.Add($"{activeBlockersCount} open task(s) currently blocking downstream deliverables.");
        }

        if (overdueTasks > 0)
        {
            summary.RiskReasons.Add($"{overdueTasks} active task(s) are past their target due date.");
        }

        var breachedMilestones = milestones
            .Where(m => m.Status is MilestoneStatus.Active or MilestoneStatus.Planned && m.EndDateUtc.Date < today)
            .ToList();

        if (breachedMilestones.Count > 0)
        {
            foreach (var bm in breachedMilestones)
            {
                summary.RiskReasons.Add($"Milestone '{bm.Name}' target end date ({bm.EndDateUtc:yyyy-MM-dd}) has passed without completion sign-off.");
            }
        }

        // Determine traffic light health
        if (overdueBlockersCount > 0 || breachedMilestones.Count > 0 || overdueTasks > 3)
        {
            summary.Health = ProjectHealthRating.Critical;
        }
        else if (activeBlockersCount > 0 || overdueTasks > 0)
        {
            summary.Health = ProjectHealthRating.AtRisk;
        }
        else
        {
            summary.Health = ProjectHealthRating.OnTrack;
        }

        return summary;
    }

    public async Task<MilestoneHealthSummaryDto> GetMilestoneHealthAsync(Guid milestoneId, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId ?? string.Empty;
        var today = DateTime.UtcNow.Date;

        var milestone = await _context.Milestones
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == milestoneId, ct);

        if (milestone == null)
        {
            return new MilestoneHealthSummaryDto
            {
                MilestoneId = milestoneId,
                MilestoneName = "Unknown Milestone",
                Health = ProjectHealthRating.OnTrack
            };
        }

        await _projectAuth.EnsureCanVisitAsync(milestone.ProjectId, operation: "GetMilestoneHealth", ct: ct);

        var tasks = await _context.Tasks
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.ProjectId == milestone.ProjectId && t.MilestoneId == milestoneId)
            .ToListAsync(ct);

        var taskIds = tasks.Select(t => t.Id).ToHashSet();

        var blockingDependencies = await _context.TaskDependencies
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.ProjectId == milestone.ProjectId && taskIds.Contains(d.TaskId) && d.DependencyType == TaskDependencyType.Blocks)
            .ToListAsync(ct);

        var blockerTaskIds = blockingDependencies.Select(d => d.DependsOnTaskId).Distinct().ToList();
        var blockerTasks = await _context.Tasks
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && blockerTaskIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, ct);

        var activeBlockersCount = 0;
        var overdueBlockersCount = 0;

        foreach (var dep in blockingDependencies)
        {
            if (blockerTasks.TryGetValue(dep.DependsOnTaskId, out var bt))
            {
                if (!bt.IsClosed)
                {
                    activeBlockersCount++;
                    if (dep.ResolveByUtc.HasValue && dep.ResolveByUtc.Value.Date < today)
                    {
                        overdueBlockersCount++;
                    }
                }
            }
        }

        // Predecessor milestone evaluations
        var milestoneDeps = await _context.MilestoneDependencies
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.MilestoneId == milestoneId)
            .ToListAsync(ct);

        var predecessorIds = milestoneDeps.Select(d => d.DependsOnMilestoneId).Distinct().ToList();
        var predecessors = await _context.Milestones
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && predecessorIds.Contains(m.Id))
            .ToListAsync(ct);

        var hasOverduePredecessors = false;
        var riskReasons = new List<string>();
        var predecessorNames = new List<string>();

        foreach (var pred in predecessors)
        {
            predecessorNames.Add(pred.Name);
            if (pred.Status != MilestoneStatus.Completed && pred.EndDateUtc.Date < today)
            {
                hasOverduePredecessors = true;
                riskReasons.Add($"Predecessor milestone '{pred.Name}' end date expired ({pred.EndDateUtc:yyyy-MM-dd}) and is not completed.");
            }
        }

        var totalTasks = tasks.Count(t => t.Status != UserTaskStatus.Cancelled);
        var completedTasks = tasks.Count(t => t.Status == UserTaskStatus.Closed);
        var inProgressTasks = tasks.Count(t => t.Status == UserTaskStatus.InProgress);
        var overdueTasks = tasks.Count(t => !t.IsClosed && t.DueDate.Date < today);
        var daysRemaining = (int)Math.Ceiling((milestone.EndDateUtc.Date - today).TotalDays);

        if (overdueBlockersCount > 0)
        {
            riskReasons.Add($"{overdueBlockersCount} blocking dependency deadline(s) are overdue.");
        }
        else if (activeBlockersCount > 0)
        {
            riskReasons.Add($"{activeBlockersCount} active blocker(s) impeding milestone tasks.");
        }

        if (overdueTasks > 0)
        {
            riskReasons.Add($"{overdueTasks} task(s) assigned to this milestone are past their due date.");
        }

        if (milestone.Status != MilestoneStatus.Completed && daysRemaining < 0)
        {
            riskReasons.Add($"Milestone target deadline has passed ({Math.Abs(daysRemaining)} days overdue).");
        }

        // Health Rating calculation
        ProjectHealthRating health;
        if (milestone.Status == MilestoneStatus.Completed)
        {
            health = ProjectHealthRating.OnTrack;
        }
        else if (daysRemaining < 0 || overdueBlockersCount > 0 || (overdueTasks > 2 && daysRemaining <= 3))
        {
            health = ProjectHealthRating.Critical;
        }
        else if (activeBlockersCount > 0 || overdueTasks > 0 || hasOverduePredecessors || (daysRemaining <= 3 && completedTasks < totalTasks))
        {
            health = ProjectHealthRating.AtRisk;
        }
        else
        {
            health = ProjectHealthRating.OnTrack;
        }

        return new MilestoneHealthSummaryDto
        {
            MilestoneId = milestone.Id,
            MilestoneName = milestone.Name,
            StartDateUtc = milestone.StartDateUtc,
            EndDateUtc = milestone.EndDateUtc,
            Status = milestone.Status,
            Health = health,
            TotalTasks = totalTasks,
            CompletedTasks = completedTasks,
            InProgressTasks = inProgressTasks,
            OverdueTasks = overdueTasks,
            ActiveBlockersCount = activeBlockersCount,
            DaysRemaining = daysRemaining,
            HasOverduePredecessors = hasOverduePredecessors,
            PredecessorMilestoneNames = predecessorNames,
            RiskReasons = riskReasons
        };
    }

    public async Task<IReadOnlyList<MilestoneHealthSummaryDto>> GetProjectMilestonesHealthAsync(Guid projectId, CancellationToken ct = default)
    {
        await _projectAuth.EnsureCanVisitAsync(projectId, operation: "GetProjectMilestonesHealth", ct: ct);

        var tenantId = _tenantContext.TenantId ?? string.Empty;

        var milestoneIds = await _context.Milestones
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.ProjectId == projectId)
            .OrderBy(m => m.OrderIndex)
            .ThenBy(m => m.StartDateUtc)
            .Select(m => m.Id)
            .ToListAsync(ct);

        var results = new List<MilestoneHealthSummaryDto>();
        foreach (var mid in milestoneIds)
        {
            var summary = await GetMilestoneHealthAsync(mid, ct);
            results.Add(summary);
        }

        return results;
    }
}
