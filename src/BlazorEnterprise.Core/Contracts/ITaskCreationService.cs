using System;
using System.Threading;
using System.Threading.Tasks;
using BlazorEnterprise.Core.Domain.Base;
using BlazorEnterprise.Core.Domain.Tasks;
using BlazorEnterprise.Core.Dtos.Response;
using BlazorEnterprise.Core.Dtos.Tasks;
using BlazorEnterprise.Shared.DataListTypes;

namespace BlazorEnterprise.Core.Contracts;

public interface ITaskCreationService
{
    /// <summary>
    /// Creates and persists a new work item / task subtype atomically within a Unit of Work transaction.
    /// </summary>
    Task<Result<BaseTaskEntity>> CreateTaskAsync(TaskCreationRequestDto request, CancellationToken ct = default);

    /// <summary>
    /// Validates the form data according to the selected task type and business invariants.
    /// </summary>
    Task<Result<bool>> ValidateTaskDataAsync(TaskCreationRequestDto request, CancellationToken ct = default);

    /// <summary>
    /// Retrieves dropdown lookup data (open sprints, project members, milestones) for a given project.
    /// </summary>
    Task<TaskCreationRelatedDataDto> GetRelatedDataAsync(Guid projectId, CreationTaskTypeEnum taskType, CancellationToken ct = default);

    /// <summary>
    /// Computes risk exposure score (1 to 12).
    /// </summary>
    int CalculateRiskExposure(RiskProbabilityEnum probability, RiskImpactEnum impact);

    /// <summary>
    /// Returns the user-friendly display name of a task subtype.
    /// </summary>
    string GetTaskTypeDisplayName(CreationTaskTypeEnum taskType);
}
