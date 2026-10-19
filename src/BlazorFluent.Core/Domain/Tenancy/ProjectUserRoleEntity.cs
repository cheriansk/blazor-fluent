using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Core.Domain.Base;
using BlazorFluent.Core.Domain.Identity;

namespace BlazorFluent.Core.Domain.Tenancy;

/// <summary>
/// Represents the role assigned to a user within a specific project under a tenant.
/// Scoped to a tenant via <see cref="TenantAuditableEntity"/> and a project via <see cref="IProjectScopedEntity"/>.
/// Implements <see cref="ISoftDeletableEntity"/> so revoked roles preserve historical audit trails.
/// </summary>
public class ProjectUserRoleEntity : TenantAuditableEntity, ISoftDeletableEntity, IProjectScopedEntity
{
    /// <summary>
    /// ID of the project to which this role assignment belongs.
    /// </summary>
    public required Guid ProjectId { get; set; }

    /// <summary>
    /// Primary identifier of the user (corresponds to <see cref="BlazorFluent.Core.Contracts.ICurrentUser.UserId"/>).
    /// </summary>
    public required string UserId { get; set; }

    /// <summary>
    /// User's email address at the time of assignment for display and audit.
    /// </summary>
    public required string UserEmail { get; set; }

    /// <summary>
    /// User's full name at the time of assignment.
    /// </summary>
    public required string UserName { get; set; }

    /// <summary>
    /// User type classification (CompanyUser or ClientUser).
    /// </summary>
    public required UserType UserType { get; set; }

    /// <summary>
    /// The assigned project role (ReadOnly, QA, Dev, Admin).
    /// </summary>
    public required ProjectRole Role { get; set; }

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
