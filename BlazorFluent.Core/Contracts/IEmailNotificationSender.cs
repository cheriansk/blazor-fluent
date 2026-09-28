using BlazorFluent.Core.Domain.Notifications;

namespace BlazorFluent.Core.Contracts;

public interface IEmailNotificationSender
{
    /// <summary>
    /// Dispatches an email notification to the specified mailbox address.
    /// </summary>
    Task<bool> SendEmailNotificationAsync(NotificationEntity notification, string recipientMailbox, CancellationToken ct = default);
}
