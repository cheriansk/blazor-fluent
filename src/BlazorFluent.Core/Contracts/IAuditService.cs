using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Auditing;
using BlazorFluent.Core.Dtos.Requests;
using BlazorFluent.Core.Dtos.Response;

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
    Task<PagedResultRespDto<AuditRecordEntity>> GetAuditTrailAsync(AuditTrailFilterReqDto filter, CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Records an unhandled application or UI exception to both the forensic audit trail (audit.AuditRecords)
    /// and structured Serilog stream.
    /// Returns a sanitized correlation incident reference (e.g., 'ERR-8F2B1C') safe to display to end users.
    /// </summary>
    Task<string> LogExceptionAsync(
        Exception exception,
        string? contextDescription = null,
        AuditSeverity severity = AuditSeverity.Error,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves property-level mutation history for a specific entity or field.
    /// </summary>
    Task<List<EntityFieldHistoryRespDto>> GetEntityHistoryAsync(
        string entityName,
        string entityId,
        string? propertyName = null,
        CancellationToken cancellationToken = default);
}

