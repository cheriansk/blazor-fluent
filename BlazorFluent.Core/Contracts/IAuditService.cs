using BlazorFluent.Core.Common;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Auditing;

namespace BlazorFluent.Core.Contracts;

/// <summary>
/// Service contract for recording and querying application, security, and tenant audit events.
/// Entity-level property diffs are captured automatically by the EF Core interceptor.
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Queries historical audit records with filtering and pagination.
    /// </summary>
    Task<PagedResult<AuditRecordEntity>> GetAuditTrailAsync(AuditTrailFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a tenant switch operation performed by an authorized company user.
    /// </summary>
    Task LogTenantSwitchAsync(string fromTenantId, string toTenantId, string? reason = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a security event (login success/failure, permission denied, policy violation).
    /// </summary>
    Task LogSecurityEventAsync(string action, AuditSeverity severity, string? details = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a critical business or user action.
    /// </summary>
    Task LogUserActivityAsync(string action, string? details = null, CancellationToken cancellationToken = default);
}
