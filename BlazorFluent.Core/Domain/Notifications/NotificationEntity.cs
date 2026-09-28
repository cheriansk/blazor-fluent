using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Delegates;

namespace BlazorFluent.Core.Domain.Notifications;

/// <summary>
/// Persisted notification entity enforcing three-tier isolation:
/// - Tenant isolation (ITenantEntity / TenantAuditableEntity)
/// - Project isolation (IProjectScopedEntity)
/// - Personal recipient isolation (UserId)
/// </summary>
public class NotificationEntity : TenantAuditableEntity, ISoftDeletableEntity, IProjectScopedEntity
{
    /// <summary>
    /// Associated project ID, or Guid.Empty for system/tenant-wide generic notifications.
    /// </summary>
    public Guid ProjectId { get; set; } = Guid.Empty;

    /// <summary>
    /// Target user ID. If null, this is a generic notification broadcast to authorized project/tenant members.
    /// If populated, it is strictly private and visible only to this specific user.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Category determining generic vs personal segmentation.
    /// </summary>
    public NotificationCategory Category { get; set; } = NotificationCategory.Generic;

    /// <summary>
    /// Visual severity level.
    /// </summary>
    public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;

    /// <summary>
    /// Headline title of the notification.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Detailed message text or summary.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Optional in-app navigation URL (e.g. "/jobs", "/tenancy/project-roles").
    /// </summary>
    public string? LinkUrl { get; set; }

    /// <summary>
    /// Whether the active recipient has marked this notification as read.
    /// </summary>
    public bool IsRead { get; set; }

    /// <summary>
    /// Timestamp when this notification was marked as read.
    /// </summary>
    public DateTime? ReadAtUtc { get; set; }

    // External Channel Dispatch Tracking
    public bool SentToTeams { get; set; }
    public DateTime? TeamsSentAtUtc { get; set; }

    public bool SentToMailbox { get; set; }
    public DateTime? MailboxSentAtUtc { get; set; }

    /// <summary>
    /// Optional serialized JSON metadata or contextual payload.
    /// </summary>
    public string? MetadataJson { get; set; }

    // ISoftDeletableEntity implementation
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}
