using BlazorFluent.Core.DataListTypes;

namespace BlazorFluent.Core.Contracts;

/// <summary>
/// Real-time health evaluation engine contract for projects and milestones.
/// Evaluates live task completion velocity, schedule variance, active blockers, and root-cause risk reasons.
/// </summary>
public interface IProjectHealthService
{
    /// <summary>
    /// Computes overall project health across all deliverables (milestone deliverables and ad-hoc tasks).
    /// </summary>
    Task<ProjectHealthSummaryDto> GetProjectHealthAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Computes milestone-specific health and schedule progress for a given milestone.
    /// </summary>
    Task<MilestoneHealthSummaryDto> GetMilestoneHealthAsync(Guid milestoneId, CancellationToken ct = default);

    /// <summary>
    /// Computes health summaries for all milestones within a project.
    /// </summary>
    Task<IReadOnlyList<MilestoneHealthSummaryDto>> GetProjectMilestonesHealthAsync(Guid projectId, CancellationToken ct = default);
}
