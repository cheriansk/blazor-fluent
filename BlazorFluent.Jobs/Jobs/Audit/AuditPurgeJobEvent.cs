using BlazorFluent.Core.Events;

namespace BlazorFluent.Jobs.Jobs.Audit;

/// <summary>
/// Triggers the nightly forensic audit log purge.
/// Deletes audit.AuditRecords older than the configured retention window.
/// TenantId is null: this is a host-level cross-tenant maintenance job.
/// </summary>
public record AuditPurgeJobEvent(
    string TriggerSource,
    string TenantId,
    string CorrelationId,
    string SenderOrigin,
    string SenderUserId,
    string SenderUserEmail,
    int RetentionDays = 365,
    Guid? ParentExecutionId = null)
    : BaseJobEvent(TriggerSource, TenantId, CorrelationId, SenderOrigin, SenderUserId, SenderUserEmail, ParentExecutionId);
