using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography;
using System.Text;
using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Core.Domain.Base;
using BlazorFluent.Core.Domain.Identity;

namespace BlazorFluent.Core.Domain.Tenancy;

/// <summary>
/// Represents a user's explicit membership and organizational role within a tenant.
/// Enables users to belong to multiple client tenants with least-privilege Zero Trust gating.
/// Enforces 3NF relational normalization by persisting only <see cref="TenantAuditableEntity.TenantId"/> and <see cref="UserEntityId"/>
/// while exposing <see cref="NotMappedAttribute"/> convenience properties from <see cref="User"/>.
/// Includes HMAC-SHA256 row-integrity anti-tampering seal.
/// </summary>
public class TenantUserEntity : TenantAuditableEntity, ISoftDeletableEntity, IHmacSignableEntity
{
    public required Guid UserEntityId { get; set; }

    public required TenantRoleEnum Role { get; set; }

    public bool IsActive { get; set; }

    /// <summary>
    /// Cryptographic HMAC-SHA256 signature sealing tenant membership and role state against direct database tampering.
    /// </summary>
    public string? RowSignature { get; set; }

    // --- ISoftDeletableEntity ---
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }

    // --- Navigation ---
    public TenantEntity? Tenant { get; set; }
    public UserEntity? User { get; set; }

    // --- [NotMapped] Navigation Forwarders ---
    [NotMapped]
    public Guid TenantEntityId { get => TenantId; set => TenantId = value; }

    [NotMapped]
    public string UserId => UserEntityId.ToString();

    [NotMapped]
    public string UserEmail => User?.Email ?? string.Empty;

    [NotMapped]
    public string UserName => !string.IsNullOrWhiteSpace(User?.FullName) ? User.FullName : (User?.Email ?? string.Empty);

    /// <summary>
    /// Computes the HMAC-SHA256 signature for this tenant user's security and role properties.
    /// </summary>
    public string ComputeIntegritySignature(string secretKey)
    {
        var payload = $"{Id}:{TenantId}:{UserEntityId}:{(int)Role}:{IsActive}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Validates whether the row's stored signature matches the expected HMAC in constant time.
    /// </summary>
    public bool VerifyIntegritySignature(string secretKey)
    {
        if (string.IsNullOrWhiteSpace(RowSignature)) return false;
        var expected = ComputeIntegritySignature(secretKey);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(RowSignature),
            Encoding.UTF8.GetBytes(expected));
    }
}
