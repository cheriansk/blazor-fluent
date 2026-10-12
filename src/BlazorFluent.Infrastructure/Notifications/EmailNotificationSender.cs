using System.Net;
using System.Net.Mail;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Infrastructure.Notifications;

public class EmailNotificationSender : IEmailNotificationSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailNotificationSender> _logger;

    public EmailNotificationSender(
        IConfiguration configuration,
        ILogger<EmailNotificationSender> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> SendEmailNotificationAsync(NotificationEntity notification, string recipientMailbox, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(recipientMailbox))
        {
            _logger.LogWarning("Email notification skipped: Recipient mailbox is empty.");
            return false;
        }

        var isEnabled = _configuration.GetValue<bool>("Notifications:Email:Enabled");
        var smtpHost = _configuration["Notifications:Email:SmtpHost"];
        if (!isEnabled || string.IsNullOrWhiteSpace(smtpHost))
        {
            _logger.LogInformation("Email notification disabled or SmtpHost unconfigured. Skipping email dispatch to {Mailbox}", recipientMailbox);
            return false;
        }

        try
        {
            var smtpPort = _configuration.GetValue<int>("Notifications:Email:SmtpPort", 587);
            var useSsl = _configuration.GetValue<bool>("Notifications:Email:UseSsl", true);
            var senderEmail = _configuration["Notifications:Email:SenderEmail"] ?? "notifications@blazorfluent.local";
            var senderName = _configuration["Notifications:Email:SenderName"] ?? "BlazorFluent Notifications";
            var username = _configuration["Notifications:Email:Username"];
            var password = _configuration["Notifications:Email:Password"];

            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                EnableSsl = useSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
            {
                client.Credentials = new NetworkCredential(username, password);
            }

            var colorHex = notification.Severity switch
            {
                NotificationSeverity.Success => "#107c41",
                NotificationSeverity.Warning => "#d83b01",
                NotificationSeverity.Error => "#a80000",
                _ => "#0076d7"
            };

            var htmlBody = $@"
<div style=""font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e1dfdd; border-radius: 6px; overflow: hidden;"">
    <div style=""background-color: {colorHex}; color: #ffffff; padding: 16px 20px;"">
        <h2 style=""margin: 0; font-size: 18px;"">[{notification.Severity.ToString().ToUpperInvariant()}] {notification.Title}</h2>
    </div>
    <div style=""padding: 20px; background-color: #ffffff; color: #323130;"">
        <p style=""font-size: 14px; line-height: 1.5; margin: 0 0 16px 0;"">{System.Net.WebUtility.HtmlEncode(notification.Message)}</p>
        <p style=""font-size: 12px; color: #605e5c; margin: 0 0 16px 0;"">Tenant: <strong>{notification.TenantId}</strong> | Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>
        {(string.IsNullOrWhiteSpace(notification.LinkUrl) ? "" : $@"<div style=""margin-top: 20px;""><a href=""{notification.LinkUrl}"" style=""background-color: {colorHex}; color: #ffffff; padding: 10px 18px; text-decoration: none; border-radius: 4px; font-weight: 600; display: inline-block;"">View in App</a></div>")}
    </div>
</div>";

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(senderEmail, senderName),
                Subject = $"[{notification.Severity}] {notification.Title}",
                Body = htmlBody,
                IsBodyHtml = true
            };

            mailMessage.To.Add(recipientMailbox);

            await client.SendMailAsync(mailMessage, ct);
            _logger.LogInformation("Email notification sent to mailbox '{Mailbox}' for '{Title}'", recipientMailbox, notification.Title);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email notification to mailbox: {Mailbox}", recipientMailbox);
            return false;
        }
    }
}
