using System.ComponentModel.DataAnnotations.Schema;
using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Core.Domain.Base;
using BlazorFluent.Core.Domain.Identity;

namespace BlazorFluent.Core.Domain.Tenancy;

/// <summary>
/// Represents a user's explicit membership and organizational role within a tenant.
/// Enables users to belong to multiple client tenants with least-privilege Zero Trust gating.
/// Enforces 3NF relational normalization by persisting only <see cref="TenantEntityId"/> and <see cref="UserEntityId"/>
/// while exposing <see cref="NotMappedAttribute"/> convenience properties from <see cref="User"/>.
/// </summary>
public class TenantUserEntity : TenantAuditableEntity, ISoftDeletableEntity
{
    public required Guid TenantEntityId { get; set; }

    public required Guid UserEntityId { get; set; }

    public required TenantRole Role { get; set; }

    public bool IsActive { get; set; }

    // --- ISoftDeletableEntity ---
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }

    // --- Navigation ---
    public TenantEntity? Tenant { get; set; }
    public UserEntity? User { get; set; }

    // --- [NotMapped] Navigation Forwarders ---
    [NotMapped]
    public string UserId => UserEntityId.ToString();

    [NotMapped]
    public string UserEmail => User?.Email ?? string.Empty;

    [NotMapped]
    public string UserName => !string.IsNullOrWhiteSpace(User?.FullName) ? User.FullName : (User?.Email ?? string.Empty);
}
