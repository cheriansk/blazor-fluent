using BlazorFluent.Core.Common;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Tenancy;

namespace BlazorFluent.Core.Contracts;

/// <summary>
/// Domain service contract managing project milestones and milestone-to-milestone dependencies.
/// </summary>
public interface IProjectMilestoneService
{
    /// <summary>
    /// Retrieves all milestones configured for a project, ordered by sequence and start date.
    /// </summary>
    Task<IReadOnlyList<ProjectMilestoneEntity>> GetMilestonesByProjectAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a single milestone by ID.
    /// </summary>
    Task<ProjectMilestoneEntity?> GetMilestoneByIdAsync(Guid milestoneId, CancellationToken ct = default);

    /// <summary>
    /// Creates a new milestone for a project.
    /// </summary>
    Task<Result<ProjectMilestoneEntity>> CreateMilestoneAsync(ProjectMilestoneEntity milestone, CancellationToken ct = default);

    /// <summary>
    /// Updates details, status, or date boundaries of a milestone.
    /// </summary>
    Task<Result<ProjectMilestoneEntity>> UpdateMilestoneAsync(ProjectMilestoneEntity milestone, CancellationToken ct = default);

    /// <summary>
    /// Explicit human lead sign-off marking a milestone as Completed.
    /// </summary>
    Task<Result<bool>> CompleteMilestoneAsync(Guid milestoneId, CancellationToken ct = default);

    /// <summary>
    /// Removes or cancels a milestone.
    /// </summary>
    Task<Result<bool>> DeleteMilestoneAsync(Guid milestoneId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all predecessor dependency links for a milestone.
    /// </summary>
    Task<IReadOnlyList<MilestoneDependencyItemDto>> GetMilestoneDependenciesAsync(Guid milestoneId, CancellationToken ct = default);

    /// <summary>
    /// Adds a predecessor dependency between two milestones.
    /// </summary>
    Task<Result<bool>> AddMilestoneDependencyAsync(Guid milestoneId, Guid dependsOnMilestoneId, string? notes = null, CancellationToken ct = default);

    /// <summary>
    /// Removes a predecessor dependency between two milestones.
    /// </summary>
    Task<Result<bool>> RemoveMilestoneDependencyAsync(Guid dependencyId, CancellationToken ct = default);
}
