using BlazorEnterprise.Shared.DataListTypes;
using BlazorEnterprise.Core.Domain.Identity;
using BlazorEnterprise.Core.Domain.Tenancy;
using BlazorEnterprise.Core.Dtos.Response;

namespace BlazorEnterprise.Core.Contracts;

public interface ITenantService
{
    Task<IReadOnlyList<TenantEntity>> GetAllTenantsAsync(CancellationToken cancellationToken = default);
    Task<Result<TenantEntity>> CreateTenantAsync(string slug, string displayName, IEnumerable<string> internalEmailDomains, IEnumerable<string> externalEmailDomains, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<Result<TenantEntity>> CreateTenantAsync(string slug, string displayName, string internalEmailDomain, string externalEmailDomain, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<Result<TenantEntity>> CreateTenantWithAdminAsync(string slug, string displayName, IEnumerable<string> internalEmailDomains, IEnumerable<string> externalEmailDomains, DateTime startDate, DateTime endDate, string adminFullName, string adminEmail, CancellationToken cancellationToken = default);
    Task<Result> UpdateTenantStatusAsync(Guid tenantId, bool isActive, CancellationToken cancellationToken = default);
    Task<Result> UpdateTenantDatesAsync(Guid tenantId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserEntity>> GetTenantUsersAsync(string tenantSlug, CancellationToken cancellationToken = default);
    Task<Result<UserEntity>> AddTenantUserAsync(string tenantSlug, string fullName, string email, DateTime startDate, DateTime endDate, bool isActive, CancellationToken cancellationToken = default);
    Task<Result<UserEntity>> UpdateTenantUserAsync(string tenantSlug, Guid userId, string fullName, string email, DateTime startDate, DateTime endDate, bool isActive, CancellationToken cancellationToken = default);
    Task<Result<TenantUserEntity>> AssignTenantAdminRoleAsync(string tenantSlug, Guid userId, bool isTenantAdmin, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectEntity>> GetTenantProjectsAsync(string tenantSlug, CancellationToken cancellationToken = default);
    Task<Result<ProjectEntity>> CreateTenantProjectAsync(string tenantSlug, string name, string shortCode, string? location, DateTime? startDate, DateTime? endDate, string? scopeSummary, ProjectStatusEnum status, Guid? programId = null, CancellationToken cancellationToken = default);
    Task<Result<ProjectEntity>> UpdateTenantProjectAsync(string tenantSlug, Guid projectId, string name, string shortCode, string? location, DateTime? startDate, DateTime? endDate, string? scopeSummary, ProjectStatusEnum status, Guid? programId = null, CancellationToken cancellationToken = default);
    Task<Result<string>> RegenerateTenantLicenseKeyAsync(string tenantSlug, CancellationToken cancellationToken = default);
    Task<Result<bool>> IsTenantDaemonActiveAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result> SetTenantDaemonActiveAsync(Guid tenantId, bool isActive, CancellationToken cancellationToken = default);
    Task<UserEntity?> GetTenantDaemonUserAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserEntity>> GetAllUsersAsync(int limit = 100, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserEntity>> GetTenantUsersByIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectEntity>> GetProjectsByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
