using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Calendar;
using BlazorEnterprise.Core.Domain.Tasks;
using BlazorEnterprise.Core.Dtos.Response;
using BlazorEnterprise.Persistence.Context;
using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorEnterprise.Persistence.Services;

/// <summary>
/// Domain service managing project delivery gate milestones in Calendars
/// and predecessor delivery gate dependencies in WorkDependencies.
/// </summary>
public class ProjectMilestoneService : IProjectMilestoneService
{
    private static readonly Func<AppDbContext, Guid, Task<CalendarEntity?>> GetMilestoneByIdCompiled =
        EF.CompileAsyncQuery((AppDbContext ctx, Guid id) =>
            ctx.Calendars.AsNoTracking().FirstOrDefault(m => m.Id == id && !m.IsDeleted));

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ITenantContext _tenantContext;
    private readonly IProjectAuthorizationService _projectAuth;
    private readonly IAuditService _auditService;
    private readonly ILogger<ProjectMilestoneService> _logger;

    public ProjectMilestoneService(
        IDbContextFactory<AppDbContext> dbFactory,
        ITenantContext tenantContext,
        IProjectAuthorizationService projectAuth,
        IAuditService auditService,
        ILogger<ProjectMilestoneService> logger)
    {
        _dbFactory = dbFactory;
        _tenantContext = tenantContext;
        _projectAuth = projectAuth;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CalendarEntity>> GetMilestonesByProjectAsync(Guid projectId, Guid? revisionHeaderId = null, CancellationToken ct = default)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        await _projectAuth.EnsureCanVisitAsync(projectId, operation: "GetMilestonesByProject", ct: ct);

        var tenantId = _tenantContext.TenantId;

        var query = _context.Calendars
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.ProjectId == projectId && !m.IsDeleted);

        if (revisionHeaderId.HasValue && revisionHeaderId.Value != Guid.Empty)
        {
            query = query.Where(m => m.RevisionHeaderId == revisionHeaderId.Value);
        }
        else
        {
            query = query.Where(m => m.RevisionHeader!.Status == RevisionStatusEnum.Active);
        }

        return await query.OrderBy(m => m.StartDate).ToListAsync(ct);
    }

    public async Task<CalendarEntity?> GetMilestoneByIdAsync(Guid milestoneId, CancellationToken ct = default)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        var tenantId = _tenantContext.TenantId;

        var milestone = await _context.Calendars
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == milestoneId && !m.IsDeleted, ct);

        if (milestone != null)
        {
            if (milestone.ProjectId.HasValue) await _projectAuth.EnsureCanVisitAsync(milestone.ProjectId.Value, operation: "GetMilestoneById", ct: ct);
        }

        return milestone;
    }

    public async Task<Result<CalendarEntity>> CreateMilestoneAsync(CalendarEntity milestone, CancellationToken ct = default)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        try
        {
            if (milestone.ProjectId.HasValue) await _projectAuth.EnsureCanEditAsync(milestone.ProjectId.Value, minRole: ProjectRoleEnum.Contribute, operation: "CreateMilestone", ct: ct);

            if (milestone.TenantId == Guid.Empty)
            {
                milestone.TenantId = _tenantContext.TenantId;
            }

            if (milestone.EndDate.HasValue && milestone.EndDate.Value.Date < milestone.StartDate.Date)
            {
                return Result<CalendarEntity>.Failure("Milestone End Date cannot precede Start Date.");
            }

            _context.Calendars.Add(milestone);
            _context.IsTransactionScopeCommitAuthorized = true; try { await _context.SaveChangesAsync(ct); } finally { _context.IsTransactionScopeCommitAuthorized = false; }

            _logger.LogInformation("Milestone gate '{Title}' ({Id}) created in project {ProjectId}", milestone.Title, milestone.Id, milestone.ProjectId);
            return Result<CalendarEntity>.Success(milestone);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create milestone '{Title}'", milestone.Title);
            return Result<CalendarEntity>.Failure($"Failed to create milestone: {ex.Message}");
        }
    }

    public async Task<Result<CalendarEntity>> UpdateMilestoneAsync(CalendarEntity updated, CancellationToken ct = default)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        try
        {
            var tenantId = _tenantContext.TenantId;

            var milestone = await _context.Calendars
                .AsTracking()
                .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.ProjectId == updated.ProjectId && m.Id == updated.Id && !m.IsDeleted, ct);

            if (milestone == null)
            {
                return Result<CalendarEntity>.Failure("Milestone not found in the specified project.");
            }

            if (milestone.ProjectId.HasValue) await _projectAuth.EnsureCanEditAsync(milestone.ProjectId.Value, minRole: ProjectRoleEnum.Contribute, operation: "UpdateMilestone", ct: ct);

            if (updated.EndDate.HasValue && updated.EndDate.Value.Date < updated.StartDate.Date)
            {
                return Result<CalendarEntity>.Failure("Milestone End Date cannot precede Start Date.");
            }

            milestone.Title = updated.Title;
            milestone.Description = updated.Description;
            milestone.StartDate = updated.StartDate;
            milestone.EndDate = updated.EndDate;
            milestone.Status = updated.Status;
            milestone.EntryType = updated.EntryType;
            milestone.ParentProgramCalendarId = updated.ParentProgramCalendarId;

            _context.IsTransactionScopeCommitAuthorized = true; try { await _context.SaveChangesAsync(ct); } finally { _context.IsTransactionScopeCommitAuthorized = false; }
            return Result<CalendarEntity>.Success(milestone);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update milestone {Id}", updated.Id);
            return Result<CalendarEntity>.Failure($"Failed to update milestone: {ex.Message}");
        }
    }

    public async Task<Result<bool>> CompleteMilestoneAsync(Guid milestoneId, CancellationToken ct = default)
    {
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        try
        {
            var tenantId = _tenantContext.TenantId;

            var milestone = await _context.Calendars
                .AsTracking()
                .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == milestoneId && !m.IsDeleted, ct);

            if (milestone == null)
            {
                return Result<bool>.Failure("Milestone not found.");
            }

            if (milestone.ProjectId.HasValue) await _projectAuth.EnsureCanEditAsync(milestone.ProjectId.Value, minRole: ProjectRoleEnum.Admin, operation: "CompleteMilestone", ct: ct);

            milestone.Status = CalendarEntryStatusEnum.Completed;
            _context.IsTransactionScopeCommitAuthorized = true; try { await _context.SaveChangesAsync(ct); } finally { _context.IsTransactionScopeCommitAuthorized = false; }

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
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        try
        {
            var tenantId = _tenantContext.TenantId;

            var milestone = await _context.Calendars
                .AsTracking()
                .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == milestoneId && !m.IsDeleted, ct);

            if (milestone == null)
            {
                return Result<bool>.Failure("Milestone not found.");
            }

            if (milestone.ProjectId.HasValue) await _projectAuth.EnsureCanEditAsync(milestone.ProjectId.Value, minRole: ProjectRoleEnum.Contribute, operation: "DeleteMilestone", ct: ct);

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
            var deletedCount = await _context.WorkDependencies
                .Where(d => d.TenantId == tenantId &&
                            ((d.DependentType == DependencyNodeTypeEnum.CalendarGate && d.DependentItemId == milestoneId) ||
                             (d.DependsOnType == DependencyNodeTypeEnum.CalendarGate && d.DependsOnItemId == milestoneId)))
                .ExecuteDeleteAsync(ct);

            milestone.IsDeleted = true;
            milestone.DeletedAtUtc = DateTime.UtcNow;

            _context.IsTransactionScopeCommitAuthorized = true; try { await _context.SaveChangesAsync(ct); } finally { _context.IsTransactionScopeCommitAuthorized = false; }

            if (deletedCount > 0)
            {
                await _auditService.LogUserActivityAsync(
                    $"Cascaded removal of {deletedCount} dependency link(s) due to deletion of milestone '{milestone.Title}'",
                    $"ProjectId: {milestone.ProjectId}, MilestoneId: {milestoneId}",
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
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        var tenantId = _tenantContext.TenantId;

        var milestone = await _context.Calendars
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == milestoneId && !m.IsDeleted, ct);

        if (milestone == null) return Array.Empty<MilestoneDependencyItemDto>();

        if (milestone.ProjectId.HasValue) await _projectAuth.EnsureCanVisitAsync(milestone.ProjectId.Value, operation: "GetMilestoneDependencies", ct: ct);

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

        var predecessors = await _context.Calendars
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
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        try
        {
            if (milestoneId == dependsOnMilestoneId)
            {
                return Result<bool>.Failure("A milestone cannot depend on itself.");
            }

            var tenantId = _tenantContext.TenantId;

            var milestone = await _context.Calendars
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == milestoneId && !m.IsDeleted, ct);

            if (milestone == null) return Result<bool>.Failure("Successor milestone not found.");

            if (milestone.ProjectId.HasValue) await _projectAuth.EnsureCanEditAsync(milestone.ProjectId.Value, minRole: ProjectRoleEnum.Contribute, operation: "AddMilestoneDependency", ct: ct);

            var predecessor = await _context.Calendars
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
            _context.IsTransactionScopeCommitAuthorized = true; try { await _context.SaveChangesAsync(ct); } finally { _context.IsTransactionScopeCommitAuthorized = false; }

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
        await using var _context = await _dbFactory.CreateDbContextAsync(ct);
        try
        {
            var tenantId = _tenantContext.TenantId;

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
            _context.IsTransactionScopeCommitAuthorized = true; try { await _context.SaveChangesAsync(ct); } finally { _context.IsTransactionScopeCommitAuthorized = false; }

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
