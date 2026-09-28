using System.Security.Cryptography;
using System.Text;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Delegates;
using BlazorFluent.Core.Domain.Identity;
using BlazorFluent.Core.Domain.Tenancy;
using BlazorFluent.Core.Domain.ValueObjects;

namespace BlazorFluent.Core.Domain.Identity;

/// <summary>
/// Application user entity.
/// Implements <see cref="IGlobalEntity"/> so user accounts exist globally across the monolith,
/// allowing company users to participate in multiple tenants and projects.
/// Includes HMAC-SHA256 row-integrity anti-tampering seal.
/// </summary>
public class UserEntity : AuditableEntity, IGlobalEntity, ISoftDeletableEntity
{
    public string Email { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public UserType UserType { get; set; } = UserType.CompanyUser;

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Effective start date for user access window.
    /// </summary>
    public DateTime StartDateUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Expiration date for user access window.
    /// </summary>
    public DateTime EndDateUtc { get; set; } = DateTime.UtcNow.AddYears(1);

    /// <summary>
    /// Cryptographic HMAC-SHA256 signature sealing user state against direct database tampering.
    /// </summary>
    public string? RowSignature { get; set; }

    /// <summary>
    /// Default or home tenant ID for this user.
    /// </summary>
    public string? DefaultTenantId { get; set; }

    /// <summary>
    /// Embedded EF Core 10 ComplexType value object for user physical address.
    /// Inline columns without separate table or shadow foreign keys.
    /// </summary>
    public Address PhysicalAddress { get; set; } = new();

    // --- ISoftDeletableEntity ---
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }

    /// <summary>
    /// Navigation to assigned project roles.
    /// </summary>
    public ICollection<ProjectUserRoleEntity> ProjectRoles { get; set; } = new List<ProjectUserRoleEntity>();

    /// <summary>
    /// Computes the HMAC-SHA256 signature for this user's sensitive security properties.
    /// </summary>
    public string ComputeIntegritySignature(string secretKey)
    {
        var payload = $"{Id}:{DefaultTenantId ?? ""}:{Email.ToLowerInvariant()}:{IsActive}:{StartDateUtc:yyyy-MM-ddTHH:mm:ssZ}:{EndDateUtc:yyyy-MM-ddTHH:mm:ssZ}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Validates whether the row's stored signature matches the expected HMAC.
    /// </summary>
    public bool VerifyIntegritySignature(string secretKey)
    {
        if (string.IsNullOrWhiteSpace(RowSignature)) return false;
        var expected = ComputeIntegritySignature(secretKey);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(RowSignature),
            Encoding.UTF8.GetBytes(expected));
    }

    /// <summary>
    /// Evaluates all account validity rules for login: active status, start/end dates, and tamper signature.
    /// </summary>
    public bool IsValidForLogin(DateTime nowUtc, string secretKey, out string failureReason)
    {
        if (IsDeleted)
        {
            failureReason = "Account has been deleted.";
            return false;
        }

        if (!IsActive)
        {
            failureReason = "Account has been deactivated.";
            return false;
        }

        if (nowUtc < StartDateUtc)
        {
            failureReason = $"Account access has not started yet (Active from: {StartDateUtc:yyyy-MM-dd}).";
            return false;
        }

        if (nowUtc > EndDateUtc)
        {
            failureReason = $"Account access expired on {EndDateUtc:yyyy-MM-dd}.";
            return false;
        }

        // Check tamper signature if present
        if (!string.IsNullOrWhiteSpace(RowSignature) && !VerifyIntegritySignature(secretKey))
        {
            failureReason = "Security integrity violation: Database row tampering detected.";
            return false;
        }

        failureReason = string.Empty;
        return true;
    }
}
