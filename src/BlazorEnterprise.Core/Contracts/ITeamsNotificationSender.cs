using BlazorEnterprise.Core.Domain.Notifications;

namespace BlazorEnterprise.Core.Contracts;

public interface ITeamsNotificationSender
{
    /// <summary>
    /// Dispatches a notification payload to Microsoft Teams via Incoming Webhook.
    /// </summary>
    Task<bool> SendTeamsNotificationAsync(NotificationEntity notification, string webhookUrl, CancellationToken ct = default);
}
