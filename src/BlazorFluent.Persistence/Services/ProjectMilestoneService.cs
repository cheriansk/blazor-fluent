using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Tenancy;
using BlazorFluent.Core.Dtos.Response;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Persistence.Services;

/// <summary>
/// Domain service managing project milestones and milestone-to-milestone predecessor dependencies.
/// </summary>
public class ProjectMilestoneService : IProjectMilestoneService
{
    private readonly AppDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly IProjectAuthorizationService _projectAuth;
    private readonly ILogger<ProjectMilestoneService> _logger;

    public ProjectMilestoneService(
        AppDbContext context,
        ITenantContext tenantContext,
        IProjectAuthorizationService projectAuth,
        ILogger<ProjectMilestoneService> logger)
    {
        _context = context;
        _tenantContext = tenantContext;
        _projectAuth = projectAuth;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ProjectMilestoneEntity>> GetMilestonesByProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        await _projectAuth.EnsureCanVisitAsync(projectId, operation: "GetMilestonesByProject", ct: ct);

        var tenantId = _tenantContext.TenantId ?? string.Empty;

        return await _context.Milestones
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.ProjectId == projectId)
            .OrderBy(m => m.OrderIndex)
            .ThenBy(m => m.StartDateUtc)
            .ToListAsync(ct);
    }

    public async Task<ProjectMilestoneEntity?> GetMilestoneByIdAsync(Guid milestoneId, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId ?? string.Empty;

        var milestone = await _context.Milestones
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == milestoneId, ct);

        if (milestone != null)
        {
            await _projectAuth.EnsureCanVisitAsync(milestone.ProjectId, operation: "GetMilestoneById", ct: ct);
        }

        return milestone;
    }

    public async Task<Result<ProjectMilestoneEntity>> CreateMilestoneAsync(ProjectMilestoneEntity milestone, CancellationToken ct = default)
    {
        try
        {
            await _projectAuth.EnsureCanEditAsync(milestone.ProjectId, minRole: ProjectRole.Dev, operation: "CreateMilestone", ct: ct);

            if (string.IsNullOrWhiteSpace(milestone.TenantId))
            {
                milestone.TenantId = _tenantContext.TenantId ?? string.Empty;
            }

            if (milestone.EndDateUtc < milestone.StartDateUtc)
            {
                return Result<ProjectMilestoneEntity>.Failure("Milestone End Date cannot precede Start Date.");
            }

            _context.Milestones.Add(milestone);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Milestone '{Name}' ({Id}) created in project {ProjectId}", milestone.Name, milestone.Id, milestone.ProjectId);
            return Result<ProjectMilestoneEntity>.Success(milestone);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create milestone '{Name}'", milestone.Name);
            return Result<ProjectMilestoneEntity>.Failure($"Failed to create milestone: {ex.Message}");
        }
    }

    public async Task<Result<ProjectMilestoneEntity>> UpdateMilestoneAsync(ProjectMilestoneEntity updated, CancellationToken ct = default)
    {
        try
        {
            var tenantId = _tenantContext.TenantId ?? string.Empty;

            var milestone = await _context.Milestones
                .AsTracking()
                .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.ProjectId == updated.ProjectId && m.Id == updated.Id, ct);

            if (milestone == null)
            {
                return Result<ProjectMilestoneEntity>.Failure("Milestone not found in the specified project.");
            }

            await _projectAuth.EnsureCanEditAsync(milestone.ProjectId, minRole: ProjectRole.Dev, operation: "UpdateMilestone", ct: ct);

            if (updated.EndDateUtc < updated.StartDateUtc)
            {
                return Result<ProjectMilestoneEntity>.Failure("Milestone End Date cannot precede Start Date.");
            }

            milestone.Name = updated.Name;
            milestone.Description = updated.Description;
            milestone.StartDateUtc = updated.StartDateUtc;
            milestone.EndDateUtc = updated.EndDateUtc;
            milestone.Status = updated.Status;
            milestone.OrderIndex = updated.OrderIndex;

            await _context.SaveChangesAsync(ct);
            return Result<ProjectMilestoneEntity>.Success(milestone);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update milestone {Id}", updated.Id);
            return Result<ProjectMilestoneEntity>.Failure($"Failed to update milestone: {ex.Message}");
        }
    }

    public async Task<Result<bool>> CompleteMilestoneAsync(Guid milestoneId, CancellationToken ct = default)
    {
        try
        {
            var tenantId = _tenantContext.TenantId ?? string.Empty;

            var milestone = await _context.Milestones
                .AsTracking()
                .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == milestoneId, ct);

            if (milestone == null)
            {
                return Result<bool>.Failure("Milestone not found.");
            }

            await _projectAuth.EnsureCanEditAsync(milestone.ProjectId, minRole: ProjectRole.Dev, operation: "CompleteMilestone", ct: ct);

            milestone.Status = MilestoneStatus.Completed;
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Milestone '{Name}' ({Id}) marked Completed by lead in project {ProjectId}", milestone.Name, milestone.Id, milestone.ProjectId);
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
            var tenantId = _tenantContext.TenantId ?? string.Empty;

            var milestone = await _context.Milestones
                .AsTracking()
                .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == milestoneId, ct);

            if (milestone == null)
            {
                return Result<bool>.Failure("Milestone not found.");
            }

            await _projectAuth.EnsureCanEditAsync(milestone.ProjectId, minRole: ProjectRole.Dev, operation: "DeleteMilestone", ct: ct);

            // Unlink any tasks linked to this milestone (preserve tasks as ad-hoc)
            var linkedTasks = await _context.Tasks
                .AsTracking()
                .Where(t => t.TenantId == tenantId && t.ProjectId == milestone.ProjectId && t.MilestoneId == milestoneId)
                .ToListAsync(ct);

            foreach (var task in linkedTasks)
            {
                task.MilestoneId = null;
            }

            // Remove dependencies involving this milestone
            var dependencies = await _context.MilestoneDependencies
                .AsTracking()
                .Where(d => d.TenantId == tenantId && (d.MilestoneId == milestoneId || d.DependsOnMilestoneId == milestoneId))
                .ToListAsync(ct);

            _context.MilestoneDependencies.RemoveRange(dependencies);
            _context.Milestones.Remove(milestone);

            await _context.SaveChangesAsync(ct);
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
        var tenantId = _tenantContext.TenantId ?? string.Empty;

        var milestone = await _context.Milestones
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == milestoneId, ct);

        if (milestone == null) return Array.Empty<MilestoneDependencyItemDto>();

        await _projectAuth.EnsureCanVisitAsync(milestone.ProjectId, operation: "GetMilestoneDependencies", ct: ct);

        var links = await _context.MilestoneDependencies
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.MilestoneId == milestoneId)
            .ToListAsync(ct);

        if (links.Count == 0) return Array.Empty<MilestoneDependencyItemDto>();

        var predecessorIds = links.Select(l => l.DependsOnMilestoneId).Distinct().ToList();
        var predecessors = await _context.Milestones
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && predecessorIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, ct);

        var result = new List<MilestoneDependencyItemDto>();
        foreach (var link in links)
        {
            predecessors.TryGetValue(link.DependsOnMilestoneId, out var pred);
            result.Add(new MilestoneDependencyItemDto
            {
                Id = link.Id,
                MilestoneId = link.MilestoneId,
                DependsOnMilestoneId = link.DependsOnMilestoneId,
                DependsOnMilestoneName = pred?.Name ?? "Unknown Milestone",
                DependsOnStatus = pred?.Status ?? MilestoneStatus.Planned,
                DependsOnEndDateUtc = pred?.EndDateUtc ?? DateTime.MinValue
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

            var tenantId = _tenantContext.TenantId ?? string.Empty;

            var milestone = await _context.Milestones
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == milestoneId, ct);

            if (milestone == null) return Result<bool>.Failure("Successor milestone not found.");

            await _projectAuth.EnsureCanEditAsync(milestone.ProjectId, minRole: ProjectRole.Dev, operation: "AddMilestoneDependency", ct: ct);

            var predecessor = await _context.Milestones
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.ProjectId == milestone.ProjectId && m.Id == dependsOnMilestoneId, ct);

            if (predecessor == null) return Result<bool>.Failure("Predecessor milestone not found in the same project.");

            var exists = await _context.MilestoneDependencies
                .AnyAsync(d => d.TenantId == tenantId && d.MilestoneId == milestoneId && d.DependsOnMilestoneId == dependsOnMilestoneId, ct);

            if (exists) return Result<bool>.Failure("This milestone dependency already exists.");

            var dependency = new MilestoneDependencyEntity
            {
                TenantId = tenantId,
                ProjectId = milestone.ProjectId,
                MilestoneId = milestoneId,
                DependsOnMilestoneId = dependsOnMilestoneId,
                Notes = notes
            };

            _context.MilestoneDependencies.Add(dependency);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Added milestone dependency: Milestone {MilestoneId} depends on {DependsOnId}", milestoneId, dependsOnMilestoneId);
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
            var tenantId = _tenantContext.TenantId ?? string.Empty;

            var link = await _context.MilestoneDependencies
                .AsTracking()
                .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Id == dependencyId, ct);

            if (link == null) return Result<bool>.Failure("Milestone dependency not found.");

            await _projectAuth.EnsureCanEditAsync(link.ProjectId, minRole: ProjectRole.Dev, operation: "RemoveMilestoneDependency", ct: ct);

            _context.MilestoneDependencies.Remove(link);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Removed milestone dependency {DependencyId}", dependencyId);
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove milestone dependency {DependencyId}", dependencyId);
            return Result<bool>.Failure($"Failed to remove milestone dependency: {ex.Message}");
        }
    }
}
