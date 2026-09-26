using BlazorFluent.Core.Domain.Delegates;

namespace BlazorFluent.Core.Domain.Tenancy;

/// <summary>
/// A project is always scoped to exactly one tenant.
/// Implements <see cref="ITenantEntity"/> via <see cref="TenantAuditableEntity"/>.
/// Implements <see cref="ISoftDeletableEntity"/> — projects are soft-deleted, not physically removed.
/// </summary>
public class ProjectEntity : TenantAuditableEntity, ISoftDeletableEntity
{
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    // --- ISoftDeletableEntity ---
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }

    /// <summary>FK back to the owning <see cref="Tenancy.TenantEntity"/>.</summary>
    public Guid TenantEntityId { get; set; }

    /// <summary>Navigation to parent tenant.</summary>
    public TenantEntity? TenantEntity { get; set; }
}
