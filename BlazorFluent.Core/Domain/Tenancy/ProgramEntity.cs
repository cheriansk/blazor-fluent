using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Base;

namespace BlazorFluent.Core.Domain.Tenancy;

/// <summary>
/// Represents an enterprise multi-year program containing one or more projects.
/// Scoped to a tenant via <see cref="TenantAuditableEntity"/>.
/// Implements <see cref="ISoftDeletableEntity"/> — programs are soft-deleted to maintain audit trails.
/// </summary>
public class ProgramEntity : TenantAuditableEntity, ISoftDeletableEntity
{
    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTime? StartDateUtc { get; set; }

    public DateTime? TargetEndDateUtc { get; set; }

    public ProgramStatus Status { get; set; } = ProgramStatus.Active;

    public bool IsActive { get; set; } = true;

    // --- ISoftDeletableEntity ---
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }

    /// <summary>FK back to the owning <see cref="TenantEntity"/>.</summary>
    public Guid TenantEntityId { get; set; }

    /// <summary>Navigation to parent tenant.</summary>
    public TenantEntity? TenantEntity { get; set; }

    /// <summary>Navigation to member projects under this program.</summary>
    public ICollection<ProjectEntity> Projects { get; set; } = new List<ProjectEntity>();

    /// <summary>Navigation to assigned program-level governance roles.</summary>
    public ICollection<ProgramUserRoleEntity> UserRoles { get; set; } = new List<ProgramUserRoleEntity>();
}
