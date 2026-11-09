using BlazorEnterprise.Core.Domain.Sprints;
using BlazorEnterprise.Core.Dtos;
using BlazorEnterprise.Shared.DataListTypes;

namespace BlazorEnterprise.Core.Contracts;

public interface ISprintPlanningService
{
    // --- Sprint Iterations (Project Settings Timeline) ---
    Task<List<SprintDto>> GetProjectSprintsAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<SprintDto?> GetSprintByIdAsync(Guid sprintId, CancellationToken cancellationToken = default);
    Task<SprintEntity> CreateSprintAsync(Guid projectId, string sprintName, int sprintNumber, DateTime startDate, DateTime endDate, string? comments, CancellationToken cancellationToken = default);
    Task UpdateSprintAsync(Guid sprintId, string sprintName, DateTime startDate, DateTime endDate, string? comments, CancellationToken cancellationToken = default);
    Task DeleteSprintAsync(Guid sprintId, CancellationToken cancellationToken = default);
    Task<SprintStatusEnum> GetSprintStatusAsync(Guid sprintId, CancellationToken cancellationToken = default);

    // --- Sprint Summary & Metrics ---
    Task<SprintMetricsDto> GetSprintMetricsAsync(Guid? sprintId, Guid projectId, CancellationToken cancellationToken = default);
    Task<List<SprintItemDto>> GetSprintItemsAsync(Guid? sprintId, Guid projectId, CancellationToken cancellationToken = default);

    // --- Sprint Planner & Item Movement ---
    Task<List<SprintItemDto>> GetItemsReadyForSprintAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task MoveItemsToSprintAsync(List<Guid> itemIds, Guid? targetSprintId, Guid projectId, CancellationToken cancellationToken = default);
    Task<List<ResourceCapacityDto>> CalculateResourceCapacityAsync(Guid projectId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    // --- Validation & Audit ---
    Task<bool> ValidateSprintDatesAsync(Guid projectId, DateTime startDate, DateTime endDate, Guid? excludeSprintId = null, CancellationToken cancellationToken = default);
    Task<List<SprintAuditLogEntity>> GetSprintAuditHistoryAsync(Guid sprintId, CancellationToken cancellationToken = default);
}
