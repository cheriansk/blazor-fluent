using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.Domain.Calendar;
using BlazorFluent.Core.Domain.Tasks;
using BlazorFluent.Core.Dtos.Response;
using BlazorFluent.Persistence.Context;
using BlazorFluent.Shared.DataListTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Persistence.Services;

/// <summary>
/// Domain service managing project delivery gate milestones in ProjectCalendars
/// and predecessor delivery gate dependencies in WorkDependencies.
/// </summary>
public class ProjectMilestoneService : IProjectMilestoneService
{
    private readonly AppDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly IProjectAuthorizationService _projectAuth;
    private readonly IAuditService _auditService;
    private readonly ILogger<ProjectMilestoneService> _logger;

    public ProjectMilestoneService(
        AppDbContext context,
        ITenantContext tenantContext,
        IProjectAuthorizationService projectAuth,
        IAuditService auditService,
        ILogger<ProjectMilestoneService> logger)
    {
        _context = context;
        _tenantContext = tenantContext;
        _projectAuth = projectAuth;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ProjectCalendarEntity>> GetMilestonesByProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        await _projectAuth.EnsureCanVisitAsync(projectId, operation: "GetMilestonesByProject", ct: ct);

        var tenantId = _tenantContext.RequiredTenantId;

        return await _context.ProjectCalendars
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.ProjectId == projectId && !m.IsDeleted)
            .OrderBy(m => m.StartDate)
            .ToListAsync(ct);
    }

    public async Task<ProjectCalendarEntity?> GetMilestoneByIdAsync(Guid milestoneId, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.RequiredTenantId;

        var milestone = await _context.ProjectCalendars
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == milestoneId && !m.IsDeleted, ct);

        if (milestone != null)
        {
            await _projectAuth.EnsureCanVisitAsync(milestone.ProjectId, operation: "GetMilestoneById", ct: ct);
        }

        return milestone;
    }

    public async Task<Result<ProjectCalendarEntity>> CreateMilestoneAsync(ProjectCalendarEntity milestone, CancellationToken ct = default)
    {
        try
        {
            await _projectAuth.EnsureCanEditAsync(milestone.ProjectId, minRole: ProjectRoleEnum.Contribute, operation: "CreateMilestone", ct: ct);

            if (milestone.TenantId == Guid.Empty)
            {
                milestone.TenantId = _tenantContext.RequiredTenantId;
            }

            if (milestone.EndDate.HasValue && milestone.EndDate.Value.Date < milestone.StartDate.Date)
            {
                return Result<ProjectCalendarEntity>.Failure("Milestone End Date cannot precede Start Date.");
            }

            _context.ProjectCalendars.Add(milestone);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Milestone gate '{Title}' ({Id}) created in project {ProjectId}", milestone.Title, milestone.Id, milestone.ProjectId);
            return Result<ProjectCalendarEntity>.Success(milestone);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create milestone '{Title}'", milestone.Title);
            return Result<ProjectCalendarEntity>.Failure($"Failed to create milestone: {ex.Message}");
        }
    }

    public async Task<Result<ProjectCalendarEntity>> UpdateMilestoneAsync(ProjectCalendarEntity updated, CancellationToken ct = default)
    {
        try
        {
            var tenantId = _tenantContext.RequiredTenantId;

            var milestone = await _context.ProjectCalendars
                .AsTracking()
                .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.ProjectId == updated.ProjectId && m.Id == updated.Id && !m.IsDeleted, ct);

            if (milestone == null)
            {
                return Result<ProjectCalendarEntity>.Failure("Milestone not found in the specified project.");
            }

            await _projectAuth.EnsureCanEditAsync(milestone.ProjectId, minRole: ProjectRoleEnum.Contribute, operation: "UpdateMilestone", ct: ct);

            if (updated.EndDate.HasValue && updated.EndDate.Value.Date < updated.StartDate.Date)
            {
                return Result<ProjectCalendarEntity>.Failure("Milestone End Date cannot precede Start Date.");
            }

            milestone.Title = updated.Title;
            milestone.Description = updated.Description;
            milestone.StartDate = updated.StartDate;
            milestone.EndDate = updated.EndDate;
            milestone.Status = updated.Status;
            milestone.EntryType = updated.EntryType;
            milestone.ParentProgramCalendarId = updated.ParentProgramCalendarId;

            await _context.SaveChangesAsync(ct);
            return Result<ProjectCalendarEntity>.Success(milestone);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update milestone {Id}", updated.Id);
            return Result<ProjectCalendarEntity>.Failure($"Failed to update milestone: {ex.Message}");
        }
    }

    public async Task<Result<bool>> CompleteMilestoneAsync(Guid milestoneId, CancellationToken ct = default)
    {
        try
        {
            var tenantId = _tenantContext.RequiredTenantId;

            var milestone = await _context.ProjectCalendars
                .AsTracking()
                .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == milestoneId && !m.IsDeleted, ct);

            if (milestone == null)
            {
                return Result<bool>.Failure("Milestone not found.");
            }

            await _projectAuth.EnsureCanEditAsync(milestone.ProjectId, minRole: ProjectRoleEnum.Admin, operation: "CompleteMilestone", ct: ct);

            milestone.Status = CalendarEntryStatusEnum.Completed;
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Milestone gate '{Title}' ({Id}) marked Completed in project {ProjectId}", milestone.Title, milestone.Id, milestone.ProjectId);
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete milestone {Id}", milestoneId);
            return Result<bool>.Failure($"Failed to complete milestone: {ex.Message}");
        }
    }

    public async Task<Result<bool>> DeleteMilestoneAsync(Guid milestoneId, CancellationToken ct = default)
    {
        try
        {
            var tenantId = _tenantContext.RequiredTenantId;

            var milestone = await _context.ProjectCalendars
                .AsTracking()
                .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == milestoneId && !m.IsDeleted, ct);

            if (milestone == null)
            {
                return Result<bool>.Failure("Milestone not found.");
            }

            await _projectAuth.EnsureCanEditAsync(milestone.ProjectId, minRole: ProjectRoleEnum.Contribute, operation: "DeleteMilestone", ct: ct);

            // Unlink any tasks linked to this milestone (preserve tasks as ad-hoc)
            var linkedTasks = await _context.Tasks
                .AsTracking()
                .Where(t => t.TenantId == tenantId && t.ProjectId == milestone.ProjectId && t.MilestoneId == milestoneId)
                .ToListAsync(ct);

            foreach (var task in linkedTasks)
            {
                task.MilestoneId = null;
            }

            // Remove dependencies where this milestone is either Dependent or Predecessor
            var dependencies = await _context.WorkDependencies
                .AsTracking()
                .Where(d => d.TenantId == tenantId &&
                            ((d.DependentType == DependencyNodeTypeEnum.CalendarGate && d.DependentItemId == milestoneId) ||
                             (d.DependsOnType == DependencyNodeTypeEnum.CalendarGate && d.DependsOnItemId == milestoneId)))
                .ToListAsync(ct);

            _context.WorkDependencies.RemoveRange(dependencies);
            milestone.IsDeleted = true;
            milestone.DeletedAtUtc = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);

            if (dependencies.Count > 0)
            {
                await _auditService.LogUserActivityAsync(
                    $"Cascaded removal of {dependencies.Count} dependency link(s) due to deletion of milestone '{milestone.Title}'",
                    $"ProjectId: {milestone.ProjectId}, MilestoneId: {milestoneId}, DependencyIds: [{string.Join(", ", dependencies.Select(d => d.Id))}]",
                    ct);
            }

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete milestone {Id}", milestoneId);
            return Result<bool>.Failure($"Failed to delete milestone: {ex.Message}");
        }
    }

    public async Task<IReadOnlyList<MilestoneDependencyItemDto>> GetMilestoneDependenciesAsync(Guid milestoneId, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.RequiredTenantId;

        var milestone = await _context.ProjectCalendars
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == milestoneId && !m.IsDeleted, ct);

        if (milestone == null) return Array.Empty<MilestoneDependencyItemDto>();

        await _projectAuth.EnsureCanVisitAsync(milestone.ProjectId, operation: "GetMilestoneDependencies", ct: ct);

        var links = await _context.WorkDependencies
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId &&
                        d.DependentType == DependencyNodeTypeEnum.CalendarGate &&
                        d.DependentItemId == milestoneId &&
                        d.DependsOnType == DependencyNodeTypeEnum.CalendarGate &&
                        !d.IsDeleted)
            .ToListAsync(ct);

        if (links.Count == 0) return Array.Empty<MilestoneDependencyItemDto>();

        var predecessorIds = links
            .Where(l => l.DependsOnItemId.HasValue)
            .Select(l => l.DependsOnItemId!.Value)
            .Distinct()
            .ToList();

        var predecessors = await _context.ProjectCalendars
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && predecessorIds.Contains(m.Id) && !m.IsDeleted)
            .ToDictionaryAsync(m => m.Id, ct);

        var result = new List<MilestoneDependencyItemDto>();
        foreach (var link in links)
        {
            var predId = link.DependsOnItemId ?? Guid.Empty;
            predecessors.TryGetValue(predId, out var pred);
            result.Add(new MilestoneDependencyItemDto
            {
                Id = link.Id,
                MilestoneId = milestoneId,
                DependsOnMilestoneId = predId,
                DependsOnMilestoneName = pred?.Title ?? link.DependsOnSnapshotTitle ?? "Unknown Milestone",
                DependsOnStatus = pred?.Status ?? CalendarEntryStatusEnum.Scheduled,
                DependsOnEndDate = pred?.EndDate ?? link.DependsOnSnapshotDueDate
            });
        }

        return result;
    }

    public async Task<Result<bool>> AddMilestoneDependencyAsync(Guid milestoneId, Guid dependsOnMilestoneId, string? notes = null, CancellationToken ct = default)
    {
        try
        {
            if (milestoneId == dependsOnMilestoneId)
            {
                return Result<bool>.Failure("A milestone cannot depend on itself.");
            }

            var tenantId = _tenantContext.RequiredTenantId;

            var milestone = await _context.ProjectCalendars
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == milestoneId && !m.IsDeleted, ct);

            if (milestone == null) return Result<bool>.Failure("Successor milestone not found.");

            await _projectAuth.EnsureCanEditAsync(milestone.ProjectId, minRole: ProjectRoleEnum.Contribute, operation: "AddMilestoneDependency", ct: ct);

            var predecessor = await _context.ProjectCalendars
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == dependsOnMilestoneId && !m.IsDeleted, ct);

            if (predecessor == null) return Result<bool>.Failure("Predecessor milestone not found.");

            var exists = await _context.WorkDependencies
                .AnyAsync(d => d.TenantId == tenantId &&
                               d.DependentType == DependencyNodeTypeEnum.CalendarGate &&
                               d.DependentItemId == milestoneId &&
                               d.DependsOnType == DependencyNodeTypeEnum.CalendarGate &&
                               d.DependsOnItemId == dependsOnMilestoneId &&
                               !d.IsDeleted, ct);

            if (exists) return Result<bool>.Failure("This milestone dependency already exists.");

            var isConflict = predecessor.EndDate.HasValue && predecessor.EndDate.Value.Date > milestone.StartDate.Date;

            var dependency = new WorkDependencyEntity
            {
                TenantId = tenantId,
                Level = TaskLevelEnum.Project,
                ProjectId = milestone.ProjectId,
                ProgramId = milestone.ProgramId,
                DependentType = DependencyNodeTypeEnum.CalendarGate,
                DependentItemId = milestoneId,
                DependsOnType = DependencyNodeTypeEnum.CalendarGate,
                DependsOnItemId = dependsOnMilestoneId,
                DependsOnLevel = TaskLevelEnum.Project,
                DependsOnProjectId = predecessor.ProjectId,
                DependsOnProgramId = predecessor.ProgramId,
                DependsOnSnapshotTitle = predecessor.Title,
                DependsOnSnapshotStatus = predecessor.Status.ToString(),
                DependsOnSnapshotDueDate = predecessor.EndDate ?? predecessor.StartDate,
                IsScheduleConflict = isConflict,
                DependencyType = TaskDependencyTypeEnum.Blocks,
                Title = $"Delivery Gate Dependency: '{milestone.Title}' waiting on '{predecessor.Title}'",
                Description = notes ?? $"Predecessor delivery gate dependency: {predecessor.Title}",
                Priority = TaskPriorityEnum.High,
                Status = predecessor.Status == CalendarEntryStatusEnum.Completed ? TaskStatusEnum.Closed : TaskStatusEnum.Open,
                StartDate = DateTime.UtcNow,
                DueDate = predecessor.EndDate ?? milestone.StartDate,
                EndDate = predecessor.Status == CalendarEntryStatusEnum.Completed ? DateTime.UtcNow : null
            };

            _context.WorkDependencies.Add(dependency);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Added milestone delivery gate dependency: Gate {MilestoneId} depends on {DependsOnId}", milestoneId, dependsOnMilestoneId);

            await _auditService.LogUserActivityAsync(
                $"Added delivery gate predecessor dependency: '{milestone.Title}' depends on '{predecessor.Title}'",
                $"ProjectId: {milestone.ProjectId}, MilestoneId: {milestoneId}, DependsOnMilestoneId: {dependsOnMilestoneId}, Conflict: {isConflict}",
                ct);

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add milestone dependency between {MilestoneId} and {DependsOnId}", milestoneId, dependsOnMilestoneId);
            return Result<bool>.Failure($"Failed to add dependency: {ex.Message}");
        }
    }

    public async Task<Result<bool>> RemoveMilestoneDependencyAsync(Guid dependencyId, CancellationToken ct = default)
    {
        try
        {
            var tenantId = _tenantContext.RequiredTenantId;

            var link = await _context.WorkDependencies
                .AsTracking()
                .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Id == dependencyId && !d.IsDeleted, ct);

            if (link == null) return Result<bool>.Failure("Milestone dependency not found.");

            if (link.ProjectId.HasValue)
            {
                await _projectAuth.EnsureCanEditAsync(link.ProjectId.Value, minRole: ProjectRoleEnum.Contribute, operation: "RemoveMilestoneDependency", ct: ct);
            }

            link.IsDeleted = true;
            link.DeletedAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Removed milestone dependency {DependencyId}", dependencyId);

            await _auditService.LogUserActivityAsync(
                $"Removed milestone dependency link between Gate {link.DependentItemId} and Predecessor {link.DependsOnItemId}",
                $"ProjectId: {link.ProjectId}, DependencyId: {dependencyId}, DependentItemId: {link.DependentItemId}, DependsOnItemId: {link.DependsOnItemId}",
                ct);

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove milestone dependency {DependencyId}", dependencyId);
            return Result<bool>.Failure($"Failed to remove milestone dependency: {ex.Message}");
        }
    }
}
