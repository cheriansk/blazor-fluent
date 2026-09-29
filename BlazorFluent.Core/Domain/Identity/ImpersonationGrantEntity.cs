using BlazorFluent.Core.Domain.Base;

namespace BlazorFluent.Core.Domain.Identity;

/// <summary>
/// Time-bound grant allowing a SuperAdmin/Host Operator to temporarily impersonate a target user
/// for troubleshooting and permission verification.
/// Implements <see cref="IGlobalEntity"/> so it exists across all tenants.
/// </summary>
public class ImpersonationGrantEntity : AuditableEntity, IGlobalEntity, ISoftDeletableEntity, IValidationExemptEntity
{
    public string SourceAdminId { get; set; } = string.Empty;
    public string SourceAdminEmail { get; set; } = string.Empty;

    public string TargetUserId { get; set; } = string.Empty;
    public string TargetUserEmail { get; set; } = string.Empty;
    public string TargetTenantId { get; set; } = string.Empty;

    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Strict expiration timestamp (maximum 60 minutes from start).
    /// </summary>
    public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddMinutes(60);

    public string Reason { get; set; } = string.Empty;

    public bool IsRevoked { get; set; }
    public DateTime? RevokedAtUtc { get; set; }

    // --- ISoftDeletableEntity ---
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }

    /// <summary>
    /// Returns true if this grant is currently valid, unexpired, and unrevoked.
    /// </summary>
    public bool IsActive(DateTime nowUtc) => !IsRevoked && !IsDeleted && nowUtc <= ExpiresAtUtc;
}
