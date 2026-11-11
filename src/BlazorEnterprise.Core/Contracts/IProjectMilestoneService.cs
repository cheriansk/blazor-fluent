using BlazorEnterprise.Core.Domain.Calendar;
using BlazorEnterprise.Core.Dtos.Response;
using BlazorEnterprise.Shared.DataListTypes;

namespace BlazorEnterprise.Core.Contracts;

/// <summary>
/// Domain service contract managing project milestones and milestone-to-milestone delivery gate dependencies.
/// Milestones are unified into <see cref="CalendarEntity"/> (where EntryType is Milestone or a delivery gate).
/// Dependencies are unified into <see cref="BlazorEnterprise.Core.Domain.Tasks.WorkDependencyEntity"/>.
/// </summary>
public interface IProjectMilestoneService
{
    /// <summary>
    /// Retrieves all milestones configured for a project, ordered by start date.
    /// By default, queries active baseline revision rows unless a specific revisionHeaderId is provided.
    /// </summary>
    Task<IReadOnlyList<CalendarEntity>> GetMilestonesByProjectAsync(Guid projectId, Guid? revisionHeaderId = null, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a single milestone by ID.
    /// </summary>
    Task<CalendarEntity?> GetMilestoneByIdAsync(Guid milestoneId, CancellationToken ct = default);

    /// <summary>
    /// Creates a new milestone for a project.
    /// </summary>
    Task<Result<CalendarEntity>> CreateMilestoneAsync(CalendarEntity milestone, CancellationToken ct = default);

    /// <summary>
    /// Updates details, status, or date boundaries of a milestone.
    /// </summary>
    Task<Result<CalendarEntity>> UpdateMilestoneAsync(CalendarEntity milestone, CancellationToken ct = default);

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
