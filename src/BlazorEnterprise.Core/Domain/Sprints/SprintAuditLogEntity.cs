using BlazorEnterprise.Core.Domain.Base;

namespace BlazorEnterprise.Core.Domain.Sprints;

/// <summary>
/// Audit trail record tracking mutations to sprint iteration timeline metadata.
/// </summary>
public class SprintAuditLogEntity : TenantScopedEntity, IImmutableEntity, IAuditExemptEntity, IUnversionedEntity
{
    public required Guid SprintId { get; set; }

    public required string ChangeType { get; set; }

    public string? ChangedBy { get; set; }

    public DateTime ChangedDate { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public string? Details { get; set; }

    // --- Navigation Properties ---
    public SprintEntity? Sprint { get; set; }
}
