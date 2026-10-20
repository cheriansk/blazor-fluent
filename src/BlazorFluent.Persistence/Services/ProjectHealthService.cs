using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.Domain.Calendar;
using BlazorFluent.Core.Domain.Tasks;
using BlazorFluent.Persistence.Context;
using BlazorFluent.Shared.DataListTypes;
using Microsoft.EntityFrameworkCore;


namespace BlazorFluent.Persistence.Services;

/// <summary>
/// Domain service calculating real-time project delivery health ratings and root-cause risk reasons.
/// Integrates task progress, milestone delivery gate dates from ProjectCalendars,
/// and active blockers/dependencies from WorkDependencies.
/// </summary>
public class ProjectHealthService : IProjectHealthService
{
    private readonly AppDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly IProjectAuthorizationService _projectAuth;

    public ProjectHealthService(
        AppDbContext context,
        ITenantContext tenantContext,
        IProjectAuthorizationService projectAuth)
    {
        _context = context;
        _tenantContext = tenantContext;
        _projectAuth = projectAuth;
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
            .Where(t => t.TenantId == tenantId && t.ProjectId == projectId && !t.IsDeleted)
            .ToListAsync(ct);

        var milestones = await _context.ProjectCalendars
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.ProjectId == projectId && !m.IsDeleted)
            .ToListAsync(ct);

        var blockingDependencies = await _context.WorkDependencies
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId &&
                        d.ProjectId == projectId &&
                        d.DependencyType == TaskDependencyTypeEnum.Blocks &&
                        !d.IsDeleted)
            .ToListAsync(ct);

        var activeBlockersCount = 0;
        var overdueBlockersCount = 0;

        foreach (var dep in blockingDependencies)
        {
            if (!dep.IsResolved)
            {
                activeBlockersCount++;
                if (dep.DueDate.Date < today || dep.IsScheduleConflict)
                {
                    overdueBlockersCount++;
                }
            }
        }

        var totalTasks = tasks.Count(t => t.Status != TaskStatusEnum.Cancelled);
        var completedTasks = tasks.Count(t => t.Status == TaskStatusEnum.Closed);
        var inProgressTasks = tasks.Count(t => t.Status == TaskStatusEnum.InProgress);
        var openTasks = tasks.Count(t => t.Status == TaskStatusEnum.Open);
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
            TotalMilestones = milestones.Count(m => m.Status != CalendarEntryStatusEnum.Cancelled),
            CompletedMilestones = milestones.Count(m => m.Status == CalendarEntryStatusEnum.Completed)
        };

        // Root cause attribution
        if (overdueBlockersCount > 0)
        {
            summary.RiskReasons.Add($"{overdueBlockersCount} blocking dependency deadline(s) or schedule conflicts have breached without resolution.");
        }
        else if (activeBlockersCount > 0)
        {
            summary.RiskReasons.Add($"{activeBlockersCount} open dependency item(s) currently blocking downstream deliverables.");
        }

        if (overdueTasks > 0)
        {
            summary.RiskReasons.Add($"{overdueTasks} active task(s) are past their target due date.");
        }

        var breachedMilestones = milestones
            .Where(m => m.Status is CalendarEntryStatusEnum.Scheduled or CalendarEntryStatusEnum.InProgress or CalendarEntryStatusEnum.Confirmed or CalendarEntryStatusEnum.AtRisk or CalendarEntryStatusEnum.Delayed &&
                        m.EndDate.HasValue && m.EndDate.Value.Date < today)
            .ToList();

        if (breachedMilestones.Count > 0)
        {
            foreach (var bm in breachedMilestones)
            {
                summary.RiskReasons.Add($"Milestone gate '{bm.Title}' target end date ({bm.EndDate:yyyy-MM-dd}) has passed without completion sign-off.");
            }
        }

        // Determine traffic light health
        if (overdueBlockersCount > 0 || breachedMilestones.Count > 0 || overdueTasks > 3)
        {
            summary.Health = ProjectHealthRatingEnum.Critical;
        }
        else if (activeBlockersCount > 0 || overdueTasks > 0)
        {
            summary.Health = ProjectHealthRatingEnum.AtRisk;
        }
        else
        {
            summary.Health = ProjectHealthRatingEnum.OnTrack;
        }

        return summary;
    }

    public async Task<MilestoneHealthSummaryDto> GetMilestoneHealthAsync(Guid milestoneId, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId ?? string.Empty;
        var today = DateTime.UtcNow.Date;

        var milestone = await _context.ProjectCalendars
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == milestoneId && !m.IsDeleted, ct);

        if (milestone == null)
        {
            return new MilestoneHealthSummaryDto
            {
                MilestoneId = milestoneId,
                MilestoneName = "Unknown Milestone",
                Health = ProjectHealthRatingEnum.OnTrack
            };
        }

        await _projectAuth.EnsureCanVisitAsync(milestone.ProjectId, operation: "GetMilestoneHealth", ct: ct);

        var tasks = await _context.Tasks
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.ProjectId == milestone.ProjectId && t.MilestoneId == milestoneId && !t.IsDeleted)
            .ToListAsync(ct);

        var taskIds = tasks.Select(t => t.Id).ToHashSet();

        var blockingDependencies = await _context.WorkDependencies
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId &&
                        d.ProjectId == milestone.ProjectId &&
                        d.DependentType == DependencyNodeTypeEnum.TaskItem &&
                        d.DependentItemId.HasValue &&
                        taskIds.Contains(d.DependentItemId.Value) &&
                        d.DependencyType == TaskDependencyTypeEnum.Blocks &&
                        !d.IsDeleted)
            .ToListAsync(ct);

        var activeBlockersCount = 0;
        var overdueBlockersCount = 0;

        foreach (var dep in blockingDependencies)
        {
            if (!dep.IsResolved)
            {
                activeBlockersCount++;
                if (dep.DueDate.Date < today || dep.IsScheduleConflict)
                {
                    overdueBlockersCount++;
                }
            }
        }

        // Predecessor milestone evaluations from WorkDependencies
        var milestoneDeps = await _context.WorkDependencies
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId &&
                        d.DependentType == DependencyNodeTypeEnum.CalendarGate &&
                        d.DependentItemId == milestoneId &&
                        d.DependsOnType == DependencyNodeTypeEnum.CalendarGate &&
                        !d.IsDeleted)
            .ToListAsync(ct);

        var predecessorIds = milestoneDeps
            .Where(d => d.DependsOnItemId.HasValue)
            .Select(d => d.DependsOnItemId!.Value)
            .Distinct()
            .ToList();

        var predecessors = await _context.ProjectCalendars
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && predecessorIds.Contains(m.Id) && !m.IsDeleted)
            .ToListAsync(ct);

        var hasOverduePredecessors = false;
        var riskReasons = new List<string>();
        var predecessorNames = new List<string>();

        foreach (var pred in predecessors)
        {
            predecessorNames.Add(pred.Title);
            if (pred.Status != CalendarEntryStatusEnum.Completed && pred.EndDate.HasValue && pred.EndDate.Value.Date < today)
            {
                hasOverduePredecessors = true;
                riskReasons.Add($"Predecessor gate '{pred.Title}' end date expired ({pred.EndDate:yyyy-MM-dd}) and is not completed.");
            }
        }

        var totalTasks = tasks.Count(t => t.Status != TaskStatusEnum.Cancelled);
        var completedTasks = tasks.Count(t => t.Status == TaskStatusEnum.Closed);
        var inProgressTasks = tasks.Count(t => t.Status == TaskStatusEnum.InProgress);
        var overdueTasks = tasks.Count(t => !t.IsClosed && t.DueDate.Date < today);
        var daysRemaining = milestone.EndDate.HasValue ? (int)Math.Ceiling((milestone.EndDate.Value.Date - today).TotalDays) : 0;

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

        if (milestone.Status != CalendarEntryStatusEnum.Completed && daysRemaining < 0)
        {
            riskReasons.Add($"Milestone target deadline has passed ({Math.Abs(daysRemaining)} days overdue).");
        }

        // Health Rating calculation
        ProjectHealthRatingEnum health;
        if (milestone.Status == CalendarEntryStatusEnum.Completed)
        {
            health = ProjectHealthRatingEnum.OnTrack;
        }
        else if (daysRemaining < 0 || overdueBlockersCount > 0 || (overdueTasks > 2 && daysRemaining <= 3))
        {
            health = ProjectHealthRatingEnum.Critical;
        }
        else if (activeBlockersCount > 0 || overdueTasks > 0 || hasOverduePredecessors || (daysRemaining <= 3 && completedTasks < totalTasks))
        {
            health = ProjectHealthRatingEnum.AtRisk;
        }
        else
        {
            health = ProjectHealthRatingEnum.OnTrack;
        }

        return new MilestoneHealthSummaryDto
        {
            MilestoneId = milestone.Id,
            MilestoneName = milestone.Title,
            StartDate = milestone.StartDate,
            EndDate = milestone.EndDate,
            Status = milestone.Status,
            EntryType = milestone.EntryType,
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

        var milestoneIds = await _context.ProjectCalendars
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.ProjectId == projectId && !m.IsDeleted)
            .OrderBy(m => m.StartDate)
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
