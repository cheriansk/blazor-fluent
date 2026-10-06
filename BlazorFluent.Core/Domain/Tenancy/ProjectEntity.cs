using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Base;

namespace BlazorFluent.Core.Domain.Tenancy;

/// <summary>
/// A project is always scoped to exactly one tenant.
/// Implements <see cref="ITenantEntity"/> via <see cref="TenantAuditableEntity"/>.
/// Implements <see cref="ISoftDeletableEntity"/> — projects are soft-deleted, not physically removed.
/// </summary>
public class ProjectEntity : TenantAuditableEntity, ISoftDeletableEntity
{
    public string Name { get; set; } = string.Empty;

    public string ShortCode { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? Location { get; set; }

    public DateTime? TentativeStartDate { get; set; }

    public DateTime? TentativeEndDate { get; set; }

    public string? ScopeSummary { get; set; }

    public ProjectStatus Status { get; set; } = ProjectStatus.New;

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Dedicated Microsoft Teams incoming webhook URL for project-specific notifications and daily task digests.
    /// If null or empty, falls back to the system-wide default Teams webhook.
    /// </summary>
    public string? TeamsWebhookUrl { get; set; }

    // --- ISoftDeletableEntity ---
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }

    /// <summary>FK back to the owning <see cref="Tenancy.TenantEntity"/>.</summary>
    public Guid TenantEntityId { get; set; }

    /// <summary>Navigation to parent tenant.</summary>
    public TenantEntity? TenantEntity { get; set; }

    /// <summary>Navigation to project user role assignments.</summary>
    public ICollection<ProjectUserRoleEntity> UserRoles { get; set; } = new List<ProjectUserRoleEntity>();
}
