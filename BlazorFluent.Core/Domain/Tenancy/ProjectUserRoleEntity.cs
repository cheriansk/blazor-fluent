using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Base;
using BlazorFluent.Core.Domain.Identity;

namespace BlazorFluent.Core.Domain.Tenancy;

/// <summary>
/// Represents the role assigned to a user within a specific project under a tenant.
/// Scoped to a tenant via <see cref="TenantAuditableEntity"/> and a project via <see cref="IProjectScopedEntity"/>.
/// Implements <see cref="ISoftDeletableEntity"/> so revoked roles preserve historical audit trails.
/// </summary>
public class ProjectUserRoleEntity : TenantAuditableEntity, ISoftDeletableEntity, IProjectScopedEntity, IValidationExemptEntity
{
    /// <summary>
    /// ID of the project to which this role assignment belongs.
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Primary identifier of the user (corresponds to <see cref="BlazorFluent.Core.Contracts.ICurrentUser.UserId"/>).
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// User's email address at the time of assignment for display and audit.
    /// </summary>
    public string UserEmail { get; set; } = string.Empty;

    /// <summary>
    /// User's full name at the time of assignment.
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// User type classification (CompanyUser or ClientUser).
    /// </summary>
    public UserType UserType { get; set; } = UserType.CompanyUser;

    /// <summary>
    /// The assigned project role (ReadOnly, QA, Dev, Admin).
    /// </summary>
    public ProjectRole Role { get; set; } = ProjectRole.ReadOnly;

    /// <summary>
    /// Optional foreign key to <see cref="UserEntity"/> if tracked locally in the database.
    /// </summary>
    public Guid? UserEntityId { get; set; }

    // --- ISoftDeletableEntity ---
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }

    // --- Navigation Properties ---
    public ProjectEntity? Project { get; set; }
    public UserEntity? User { get; set; }
}
