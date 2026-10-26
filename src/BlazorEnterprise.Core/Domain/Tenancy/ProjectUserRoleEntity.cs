using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography;
using System.Text;
using BlazorEnterprise.Shared.DataListTypes;
using BlazorEnterprise.Core.Domain.Base;
using BlazorEnterprise.Core.Domain.Identity;

namespace BlazorEnterprise.Core.Domain.Tenancy;

/// <summary>
/// Represents the role assigned to a user within a specific project under a tenant.
/// Scoped to a tenant via <see cref="TenantAuditableEntity"/> and a project via <see cref="IProjectScopedEntity"/>.
/// Implements <see cref="ISoftDeletableEntity"/> so revoked roles preserve historical audit trails.
/// Enforces 3NF relational normalization by persisting only <see cref="ProjectId"/> and <see cref="UserEntityId"/>
/// while exposing <see cref="NotMappedAttribute"/> convenience properties from <see cref="User"/>.
/// Includes HMAC-SHA256 row-integrity anti-tampering seal.
/// </summary>
public class ProjectUserRoleEntity : TenantAuditableEntity, ISoftDeletableEntity, IProjectScopedEntity, IHmacSignableEntity
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
    public required ProjectRoleEnum Role { get; set; }

    /// <summary>
    /// Cryptographic HMAC-SHA256 signature sealing project role state against direct database tampering.
    /// </summary>
    public string? RowSignature { get; set; }

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
    public UserTypeEnum UserType => User?.UserType ?? UserTypeEnum.ClientUser;

    /// <summary>
    /// Computes the HMAC-SHA256 signature for this project role's security properties.
    /// </summary>
    public string ComputeIntegritySignature(string secretKey)
    {
        var payload = $"{Id}:{TenantId}:{ProjectId}:{UserEntityId}:{(int)Role}";
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
