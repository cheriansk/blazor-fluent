using System.Security.Cryptography;
using System.Text;
using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Core.Domain.Base;
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
public class UserEntity : AuditableEntity, IGlobalEntity, IEffectiveDatedEntity, ISoftDeletableEntity, IHmacSignableEntity
{
    public required string Email { get; set; }

    public required string FullName { get; set; }

    public required UserTypeEnum UserType { get; set; }

    /// <summary>
    /// Architectural classification: Human, SystemDaemon, or ApiIntegrationBot.
    /// </summary>
    public required UserKindEnum UserKind { get; set; }

    public bool IsSystemDaemon => UserKind == UserKindEnum.SystemDaemon;

    public bool IsActive { get; set; }

    /// <summary>
    /// Effective start date for user access window (UTC).
    /// Part of <see cref="IEffectiveDatedEntity"/>.
    /// </summary>
    public required DateTime StartDate { get; set; }

    /// <summary>
    /// Expiration date for user access window (UTC).
    /// Part of <see cref="IEffectiveDatedEntity"/>.
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Cryptographic HMAC-SHA256 signature sealing user state against direct database tampering.
    /// </summary>
    public string? RowSignature { get; set; }

    /// <summary>
    /// User's preferred timezone identifier (e.g., 'Eastern Standard Time', 'UTC').
    /// Used for presentation-layer date/time formatting.
    /// </summary>
    public required string TimeZoneId { get; set; }

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
    /// Navigation to assigned program governance roles.
    /// </summary>
    public ICollection<ProgramUserRoleEntity> ProgramRoles { get; set; } = new List<ProgramUserRoleEntity>();

    /// <summary>
    /// Navigation to tenant memberships.
    /// </summary>
    public ICollection<TenantUserEntity> TenantMemberships { get; set; } = new List<TenantUserEntity>();

    /// <summary>
    /// Computes the HMAC-SHA256 signature for this user's sensitive security properties.
    /// </summary>
    public string ComputeIntegritySignature(string secretKey)
    {
        var startUtc = StartDate.Kind == DateTimeKind.Utc
            ? StartDate
            : (StartDate.Kind == DateTimeKind.Local ? StartDate.ToUniversalTime() : DateTime.SpecifyKind(StartDate, DateTimeKind.Utc));

        var endUtc = EndDate.HasValue
            ? (EndDate.Value.Kind == DateTimeKind.Utc
                ? EndDate.Value
                : (EndDate.Value.Kind == DateTimeKind.Local ? EndDate.Value.ToUniversalTime() : DateTime.SpecifyKind(EndDate.Value, DateTimeKind.Utc)))
            : DateTime.MaxValue;

        var payload = $"{Id}:{Email.ToLowerInvariant()}:{IsActive}:{startUtc:yyyy-MM-ddTHH:mm:ssZ}:{endUtc:yyyy-MM-ddTHH:mm:ssZ}:{UserKind}";
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

        if (UserKind != UserKindEnum.Human) { failureReason = "System daemon and bot accounts cannot log in."; return false; } if (!IsActive)
        {
            failureReason = "Account has been deactivated.";
            return false;
        }

        var todayUtc = nowUtc.Date;

        if (todayUtc < StartDate.Date)
        {
            failureReason = $"Account access has not started yet (Active from: {StartDate:yyyy-MM-dd}).";
            return false;
        }

        if (EndDate.HasValue && todayUtc > EndDate.Value.Date)
        {
            failureReason = $"Account access expired on {EndDate.Value:yyyy-MM-dd}.";
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
