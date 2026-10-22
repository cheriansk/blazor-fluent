using BlazorFluent.Core.Events;
using BlazorFluent.Core.Utilities;

namespace BlazorFluent.Jobs.Jobs.Audit;

/// <summary>
/// Triggers the nightly forensic audit log purge.
/// Deletes audit.AuditRecords older than the configured retention window.
/// Defaults to SystemIdentityUtility.SystemDaemonTenantId for host-level cross-tenant maintenance.
/// </summary>
public record AuditPurgeJobEvent(
    string TriggerSource,
    Guid TenantId,
    string CorrelationId,
    string SenderOrigin,
    string SenderUserId,
    string SenderUserEmail,
    int RetentionDays = 365,
    Guid? ParentExecutionId = null)
    : BaseJobEvent(
        TriggerSource,
        TenantId != Guid.Empty ? TenantId : SystemIdentityUtility.SystemDaemonTenantId,
        CorrelationId,
        SenderOrigin,
        SenderUserId,
        SenderUserEmail,
        ParentExecutionId);
