using BlazorFluent.Core.Common;
using BlazorFluent.Core.Domain.Identity;
using BlazorFluent.Core.Domain.Tenancy;

namespace BlazorFluent.Core.Contracts;

public interface ITenantService
{
    Task<IReadOnlyList<TenantEntity>> GetAllTenantsAsync(CancellationToken cancellationToken = default);
    Task<Result<TenantEntity>> CreateTenantAsync(string slug, string code, string displayName, IEnumerable<string> internalEmailDomains, IEnumerable<string> externalEmailDomains, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<Result<TenantEntity>> CreateTenantAsync(string slug, string code, string displayName, string internalEmailDomain, string externalEmailDomain, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<Result> UpdateTenantStatusAsync(Guid tenantId, bool isActive, CancellationToken cancellationToken = default);
    Task<Result> UpdateTenantDatesAsync(Guid tenantId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserEntity>> GetTenantUsersAsync(string tenantSlug, CancellationToken cancellationToken = default);
    Task<Result<UserEntity>> AddTenantUserAsync(string tenantSlug, string fullName, string email, DateTime startDate, DateTime endDate, bool isActive, CancellationToken cancellationToken = default);
    Task<Result<UserEntity>> UpdateTenantUserAsync(string tenantSlug, Guid userId, string fullName, string email, DateTime startDate, DateTime endDate, bool isActive, CancellationToken cancellationToken = default);
}
