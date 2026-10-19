using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Core.Domain.Base;

namespace BlazorFluent.Core.Domain.Tenancy;

/// <summary>
/// Represents an enterprise multi-year program containing one or more projects.
/// Scoped to a tenant via <see cref="TenantAuditableEntity"/>.
/// Implements <see cref="ISoftDeletableEntity"/> — programs are soft-deleted to maintain audit trails.
/// </summary>
public class ProgramEntity : TenantAuditableEntity, ISoftDeletableEntity
{
    public required string Name { get; set; }

    public required string Code { get; set; }

    public string? Description { get; set; }

    public DateTime? StartDateUtc { get; set; }

    public DateTime? TargetEndDateUtc { get; set; }

    public required ProgramStatus Status { get; set; }

    public bool IsActive { get; set; }

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
