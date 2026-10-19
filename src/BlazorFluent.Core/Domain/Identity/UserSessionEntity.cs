using BlazorFluent.Core.Domain.Base;

namespace BlazorFluent.Core.Domain.Identity;

/// <summary>
/// Tracks active user circuits and browser sessions for visibility and remote revocation.
/// </summary>
public class UserSessionEntity : TenantAuditableEntity, ISoftDeletableEntity
{
    public required string UserId { get; set; }
    public required string UserEmail { get; set; }
    public required string IpAddress { get; set; }
    public required string UserAgent { get; set; }

    public required DateTime StartedAtUtc { get; set; }
    public required DateTime LastActivityAtUtc { get; set; }

    public bool IsRevoked { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? RevokedBy { get; set; }

    // --- ISoftDeletableEntity ---
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}
