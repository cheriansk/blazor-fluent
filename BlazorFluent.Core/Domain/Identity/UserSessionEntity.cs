using BlazorFluent.Core.Domain.Base;

namespace BlazorFluent.Core.Domain.Identity;

/// <summary>
/// Tracks active user circuits and browser sessions for visibility and remote revocation.
/// </summary>
public class UserSessionEntity : TenantAuditableEntity, ISoftDeletableEntity, IValidationExemptEntity
{
    public string UserId { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string IpAddress { get; set; } = "127.0.0.1";
    public string UserAgent { get; set; } = string.Empty;

    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastActivityAtUtc { get; set; } = DateTime.UtcNow;

    public bool IsRevoked { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? RevokedBy { get; set; }

    // --- ISoftDeletableEntity ---
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}
