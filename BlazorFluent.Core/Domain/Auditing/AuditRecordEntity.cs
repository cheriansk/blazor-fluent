using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Delegates;

namespace BlazorFluent.Core.Domain.Auditing;

/// <summary>
/// Forensic audit trail record stored in the 'audit' PostgreSQL schema.
/// Captures property-level entity diffs, tenant-switching events, security milestones, and user activities.
/// Implements <see cref="IAuditExemptEntity"/> to prevent recursive auditing of audit log entries.
/// </summary>
public class AuditRecordEntity : AuditableEntity, ITenantEntity, IAuditExemptEntity
{
    /// <summary>
    /// The tenant scope for this audit record. Multi-tenant queries automatically row-filter by this value.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>ID of the user who performed the operation, or 'system' for background workers.</summary>
    public string? UserId { get; set; }

    /// <summary>Email or username of the acting user.</summary>
    public string? UserEmail { get; set; }

    /// <summary>Capacity of the acting user at the time of the action (CompanyUser vs ClientUser).</summary>
    public UserType? UserType { get; set; }

    /// <summary>High-level classification of the audit event.</summary>
    public AuditEventType EventType { get; set; }

    /// <summary>Severity classification.</summary>
    public AuditSeverity Severity { get; set; } = AuditSeverity.Information;

    /// <summary>Name of the entity modified (e.g. 'ProductEntity', 'ProjectEntity'), if applicable.</summary>
    public string? EntityName { get; set; }

    /// <summary>Primary key of the modified entity as a string, if applicable.</summary>
    public string? EntityId { get; set; }

    /// <summary>Database operation performed (Insert, Update, Delete), if applicable.</summary>
    public EntityOperation? Operation { get; set; }

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
}
