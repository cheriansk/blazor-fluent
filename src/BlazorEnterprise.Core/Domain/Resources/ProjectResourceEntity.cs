using System.ComponentModel.DataAnnotations.Schema;
using BlazorEnterprise.Core.Domain.Base;
using BlazorEnterprise.Core.Domain.Identity;
using BlazorEnterprise.Core.Domain.Tenancy;
using BlazorEnterprise.Shared.DataListTypes;

namespace BlazorEnterprise.Core.Domain.Resources;

/// <summary>
/// Represents a user assigned as an available project resource for staffing and allocation.
/// </summary>
public class ProjectResourceEntity : TenantAuditableEntity, ISoftDeletableEntity, IProjectScopedEntity, IUnversionedEntity
{
    public required Guid ProjectId { get; set; }

    public required Guid UserEntityId { get; set; }

    public required ProjectResourceRoleEnum Role { get; set; }

    // --- ISoftDeletableEntity ---
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }

    // --- Navigation Properties ---
    public ProjectEntity? Project { get; set; }
    public UserEntity? User { get; set; }

    [NotMapped]
    public string UserName => !string.IsNullOrWhiteSpace(User?.FullName) ? User.FullName : (User?.Email ?? string.Empty);

    [NotMapped]
    public string UserEmail => User?.Email ?? string.Empty;
}
