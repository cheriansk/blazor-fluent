using BlazorEnterprise.Shared.DataListTypes;
using BlazorEnterprise.Core.Domain.Base;

namespace BlazorEnterprise.Core.Domain.Auditing;

/// <summary>
/// Forensic audit trail record stored in the 'audit' PostgreSQL schema.
/// Captures property-level entity diffs, tenant-switching events, security milestones, and user activities.
/// Implements <see cref="IAuditExemptEntity"/> to prevent recursive auditing of audit log entries.
/// </summary>
public class AuditRecordEntity : TenantAuditableEntity, IAuditExemptEntity, IImmutableEntity, IUnversionedEntity
{
    /// <summary>ID of the user who performed the operation, or 'system' for background workers.</summary>
    public string? UserId { get; set; }

    /// <summary>Email or username of the acting user.</summary>
    public string? UserEmail { get; set; }

    /// <summary>Capacity of the acting user at the time of the action (CompanyUser vs ClientUser).</summary>
    public UserTypeEnum? UserType { get; set; }

    /// <summary>High-level classification of the audit event.</summary>
    public required AuditEventTypeEnum EventType { get; set; }

    /// <summary>Severity classification.</summary>
    public required AuditSeverityEnum Severity { get; set; }

    /// <summary>Name of the entity modified (e.g. 'TaskEntity', 'ProjectEntity'), if applicable.</summary>
    public string? EntityName { get; set; }

    /// <summary>Primary key of the modified entity as a string, if applicable.</summary>
    public string? EntityId { get; set; }

    /// <summary>Database operation performed (Insert, Update, Delete), if applicable.</summary>
    public EntityOperationEnum? Operation { get; set; }

    /// <summary>
    /// Detailed property-level diff serialized as JSON.
    /// Format for updates: { "Price": { "Old": 100.0, "New": 125.0 } }
    /// Sensitive values (passwords, tokens, keys) are automatically masked to '****'.
    /// </summary>
    public string? ChangesJson { get; set; }

    /// <summary>Human-readable description or notes (e.g., 'Switched tenant from acme to globex').</summary>
    public string? Description { get; set; }

    /// <summary>Client IP address, if captured.</summary>
    public string? IpAddress { get; set; }

    /// <summary>Correlation or distributed trace ID.</summary>
    public string? TraceId { get; set; }

    /// <summary>
    /// Configured retention window for this specific audit record.
    /// Defaults to 365 days for Security events, and 90 days for all others.
    /// Can be adjusted by authorized administrators between 90 and 730 days.
    /// </summary>
    public AuditRetentionPeriodEnum RetentionDays { get; set; } = AuditRetentionPeriodEnum.NinetyDays;
}
