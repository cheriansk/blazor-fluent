using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Core.Domain.Base;

namespace BlazorFluent.Core.Domain.Tasks;

/// <summary>
/// Explicit directional association linking two tasks.
/// When LinkType is BlockedBy, maintains a foreign key to the generated BlockerDependencyEntity.
/// </summary>
public class TaskLinkEntity : TenantAuditableEntity, ISoftDeletableEntity
{
    /// <summary>
    /// ID of the source task originating the link.
    /// </summary>
    public required Guid SourceTaskId { get; set; }

    /// <summary>
    /// ID of the target task being referenced.
    /// </summary>
    public required Guid TargetTaskId { get; set; }

    /// <summary>
    /// Classification of the relationship (BlockedBy, RelatesTo, Parent).
    /// </summary>
    public required TaskDependencyType LinkType { get; set; }

    /// <summary>
    /// Foreign key to the auto-generated BlockerDependencyEntity when LinkType is BlockedBy.
    /// </summary>
    public Guid? BlockerDependencyId { get; set; }

    // --- ISoftDeletableEntity ---
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }

    // --- Navigation Properties ---
    public TaskEntity? SourceTask { get; set; }
    public TaskEntity? TargetTask { get; set; }
    public BlockerDependencyEntity? BlockerDependency { get; set; }
}
