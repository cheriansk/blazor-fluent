using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Notifications;
using BlazorFluent.Core.Dtos;

namespace BlazorFluent.Core.Contracts;

public interface INotificationService
{
    /// <summary>
    /// Persists a notification to the database and dispatches across requested channels (Teams/Email)
    /// if matching configuration is found.
    /// </summary>
    Task<NotificationEntity> SendAsync(SendNotificationReqDto request, CancellationToken ct = default);

    /// <summary>
    /// Retrieves notifications for the active user, strictly honoring tenant, project, and personal privacy filters.
    /// </summary>
    Task<IReadOnlyList<NotificationEntity>> GetNotificationsAsync(NotificationFilterReqDto filter, CancellationToken ct = default);

    /// <summary>
    /// Computes aggregated unread counts for generic and personal categories.
    /// </summary>
    Task<UnreadNotificationCountsRespDto> GetUnreadCountsAsync(CancellationToken ct = default);

    /// <summary>
    /// Marks a specific notification as read, ensuring the caller is authorized.
    /// </summary>
    Task<bool> MarkAsReadAsync(Guid notificationId, CancellationToken ct = default);

    /// <summary>
    /// Marks all notifications in the specified category as read for the active user.
    /// </summary>
    Task<int> MarkAllAsReadAsync(NotificationCategory category, CancellationToken ct = default);
}
