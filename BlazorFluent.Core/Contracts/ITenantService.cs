using BlazorFluent.Core.Common;
using BlazorFluent.Core.Domain.Tenancy;

namespace BlazorFluent.Core.Contracts;

public interface ITenantService
{
    Task<IReadOnlyList<TenantEntity>> GetAllTenantsAsync(CancellationToken cancellationToken = default);
    Task<Result<TenantEntity>> CreateTenantAsync(string slug, string displayName, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<Result> UpdateTenantStatusAsync(Guid tenantId, bool isActive, CancellationToken cancellationToken = default);
    Task<Result> UpdateTenantDatesAsync(Guid tenantId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
}
