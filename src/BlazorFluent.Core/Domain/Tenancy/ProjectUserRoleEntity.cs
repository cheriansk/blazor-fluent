using System.ComponentModel.DataAnnotations.Schema;
using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Core.Domain.Base;
using BlazorFluent.Core.Domain.Identity;

namespace BlazorFluent.Core.Domain.Tenancy;

/// <summary>
/// Represents the role assigned to a user within a specific project under a tenant.
/// Scoped to a tenant via <see cref="TenantAuditableEntity"/> and a project via <see cref="IProjectScopedEntity"/>.
/// Implements <see cref="ISoftDeletableEntity"/> so revoked roles preserve historical audit trails.
/// Enforces 3NF relational normalization by persisting only <see cref="ProjectId"/> and <see cref="UserEntityId"/>
/// while exposing <see cref="NotMappedAttribute"/> convenience properties from <see cref="User"/>.
/// </summary>
public class ProjectUserRoleEntity : TenantAuditableEntity, ISoftDeletableEntity, IProjectScopedEntity
{
    /// <summary>
    /// ID of the project to which this role assignment belongs.
    /// </summary>
    public required Guid ProjectId { get; set; }

    /// <summary>
    /// Foreign key to <see cref="UserEntity"/>.
    /// </summary>
    public required Guid UserEntityId { get; set; }

    /// <summary>
    /// The assigned project role (ReadOnly, QA, Dev, Admin).
    /// </summary>
    public required ProjectRole Role { get; set; }

    // --- ISoftDeletableEntity ---
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }

    // --- Navigation Properties ---
    public ProjectEntity? Project { get; set; }
    public UserEntity? User { get; set; }

    // --- [NotMapped] Navigation Forwarders ---
    [NotMapped]
    public string UserId => UserEntityId.ToString();

    [NotMapped]
    public string UserEmail => User?.Email ?? string.Empty;

    [NotMapped]
    public string UserName => !string.IsNullOrWhiteSpace(User?.FullName) ? User.FullName : (User?.Email ?? string.Empty);

    [NotMapped]
    public UserType UserType => User?.UserType ?? UserType.ClientUser;
}
