using BlazorEnterprise.Core.Domain.Identity;
using BlazorEnterprise.Core.Domain.Resources;
using BlazorEnterprise.Core.Domain.Versioning;
using BlazorEnterprise.Core.Dtos;
using BlazorEnterprise.Core.Models;
using BlazorEnterprise.Shared.DataListTypes;

namespace BlazorEnterprise.Core.Contracts;

public interface IResourceManagementService
{
    // --- Resource Roster ---
    Task<List<ProjectResourceDto>> GetProjectResourcesAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<List<UserEntity>> GetAvailableProjectUsersAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<ProjectResourceEntity> AddResourceAsync(Guid projectId, Guid userEntityId, ProjectResourceRoleEnum role, CancellationToken cancellationToken = default);
    Task RemoveResourceAsync(Guid projectResourceId, CancellationToken cancellationToken = default);
    Task UpdateResourceRoleAsync(Guid projectResourceId, ProjectResourceRoleEnum newRole, CancellationToken cancellationToken = default);

    // --- Versioned Allocation Plan ---
    Task<RevisionHeaderEntity> GetOrCreateDraftAllocationPlanAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<ResourcePlanSummaryDto> GetResourcePlanSummaryAsync(Guid projectId, Guid? revisionHeaderId = null, CancellationToken cancellationToken = default);
    Task SetResourceAllocationAsync(Guid revisionHeaderId, Guid projectResourceId, DateOnly date, int allocationPercentage, CancellationToken cancellationToken = default);
    Task BulkSetAllocationAsync(Guid revisionHeaderId, Guid projectResourceId, DateOnly startDate, DateOnly endDate, int allocationPercentage, CancellationToken cancellationToken = default);
    Task CopyPreviousDayAllocationAsync(Guid revisionHeaderId, Guid projectResourceId, DateOnly targetDate, CancellationToken cancellationToken = default);

    // --- Rates & Rate Card ---
    Task<List<ProjectResourceDto>> GetRateCardAsync(Guid projectId, Guid? revisionHeaderId = null, CancellationToken cancellationToken = default);
    Task SetResourceRateAsync(Guid revisionHeaderId, Guid projectResourceId, decimal hourlyRate, string currency = "USD", CancellationToken cancellationToken = default);
    Task BulkSetRateByRoleAsync(Guid revisionHeaderId, Guid projectId, ProjectResourceRoleEnum role, decimal hourlyRate, string currency = "USD", CancellationToken cancellationToken = default);

    // --- Plan History & Status ---
    Task<List<RevisionSummaryDto>> GetAllocationPlanHistoryAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<bool> IsAllocationPlanLockedAsync(Guid revisionHeaderId, CancellationToken cancellationToken = default);
}
