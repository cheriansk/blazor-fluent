using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Core.Domain.Base;
using BlazorFluent.Core.Domain.Identity;

namespace BlazorFluent.Core.Domain.Tenancy;

/// <summary>
/// Represents a user's explicit membership and organizational role within a tenant.
/// Enables users to belong to multiple client tenants with least-privilege Zero Trust gating.
/// </summary>
public class TenantUserEntity : TenantAuditableEntity, ISoftDeletableEntity
{
    public Guid TenantEntityId { get; set; }

    public Guid UserEntityId { get; set; }

    public required string UserId { get; set; }

    public required string UserEmail { get; set; }

    public required string UserName { get; set; }

    public required TenantRole Role { get; set; }

    public bool IsActive { get; set; }

    // --- ISoftDeletableEntity ---
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }

    // --- Navigation ---
    public TenantEntity? Tenant { get; set; }
    public UserEntity? User { get; set; }
}
