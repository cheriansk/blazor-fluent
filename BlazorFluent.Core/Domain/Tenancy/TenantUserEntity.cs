using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Base;
using BlazorFluent.Core.Domain.Identity;

namespace BlazorFluent.Core.Domain.Tenancy;

/// <summary>
/// Represents a user's explicit membership and organizational role within a tenant.
/// Enables users to belong to multiple client tenants with least-privilege Zero Trust gating.
/// </summary>
public class TenantUserEntity : TenantAuditableEntity, ISoftDeletableEntity, IValidationExemptEntity
{
    public Guid TenantEntityId { get; set; }

    public Guid UserEntityId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string UserEmail { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public TenantRole Role { get; set; } = TenantRole.TenantMember;

    public bool IsActive { get; set; } = true;

    // --- ISoftDeletableEntity ---
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }

    // --- Navigation ---
    public TenantEntity? Tenant { get; set; }
    public UserEntity? User { get; set; }
}
