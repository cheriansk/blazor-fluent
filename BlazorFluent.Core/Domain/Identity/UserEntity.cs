using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Delegates;
using BlazorFluent.Core.Domain.Tenancy;

namespace BlazorFluent.Core.Domain.Identity;

/// <summary>
/// Application user entity.
/// Implements <see cref="IGlobalEntity"/> so user accounts exist globally across the monolith,
/// allowing company users to participate in multiple tenants and projects.
/// </summary>
public class UserEntity : AuditableEntity, IGlobalEntity, ISoftDeletableEntity
{
    public string Email { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public UserType UserType { get; set; } = UserType.CompanyUser;

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Default or home tenant ID for this user.
    /// </summary>
    public string? DefaultTenantId { get; set; }

    // --- ISoftDeletableEntity ---
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }

    /// <summary>
    /// Navigation to assigned project roles.
    /// </summary>
    public ICollection<ProjectUserRoleEntity> ProjectRoles { get; set; } = new List<ProjectUserRoleEntity>();
}
