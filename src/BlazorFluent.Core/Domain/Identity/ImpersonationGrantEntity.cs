using BlazorFluent.Core.Domain.Base;

namespace BlazorFluent.Core.Domain.Identity;

/// <summary>
/// Time-bound grant allowing a SuperAdmin/Host Operator to temporarily impersonate a target user
/// for troubleshooting and permission verification.
/// Implements <see cref="IGlobalEntity"/> so it exists across all tenants.
/// </summary>
public class ImpersonationGrantEntity : AuditableEntity, IGlobalEntity, ISoftDeletableEntity
{
    public required string SourceAdminId { get; set; }
    public required string SourceAdminEmail { get; set; }

    public required string TargetUserId { get; set; }
    public required string TargetUserEmail { get; set; }
    public required Guid TargetTenantId { get; set; }

    public required DateTime StartedAtUtc { get; set; }

    /// <summary>
    /// Strict expiration timestamp (maximum 60 minutes from start).
    /// </summary>
    public required DateTime ExpiresAtUtc { get; set; }

    public required string Reason { get; set; }

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
