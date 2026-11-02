using System.Net;
using System.Net.Mail;
using BlazorEnterprise.Core.Configuration;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Shared.DataListTypes;
using BlazorEnterprise.Core.Domain.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlazorEnterprise.Infrastructure.Notifications;

public class EmailNotificationSender : IEmailNotificationSender
{
    private readonly IOptions<NotificationOptions> _options;
    private readonly ILogger<EmailNotificationSender> _logger;

    public EmailNotificationSender(
        IOptions<NotificationOptions> options,
        ILogger<EmailNotificationSender> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<bool> SendEmailNotificationAsync(NotificationEntity notification, string recipientMailbox, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(recipientMailbox))
        {
            _logger.LogWarning("Email notification skipped: Recipient mailbox is empty.");
            return false;
        }

        var emailOpts = _options.Value.Email;
        var isEnabled = emailOpts.Enabled;
        var smtpHost = emailOpts.SmtpHost;
        if (!isEnabled || string.IsNullOrWhiteSpace(smtpHost))
        {
            _logger.LogInformation("Email notification disabled or SmtpHost unconfigured. Skipping email dispatch to {Mailbox}", recipientMailbox);
            return false;
        }

        try
        {
            var smtpPort = emailOpts.SmtpPort;
            var useSsl = emailOpts.UseSsl;
            var senderEmail = emailOpts.SenderEmail;
            if (string.IsNullOrWhiteSpace(senderEmail))
            {
                _logger.LogError("Zero-Trust Security Violation: 'Notifications:Email:SenderEmail' is unconfigured. Aborting email dispatch to {Mailbox}.", recipientMailbox);
                return false;
            }
            var senderName = !string.IsNullOrWhiteSpace(emailOpts.SenderName) ? emailOpts.SenderName : "BlazorEnterprise Notifications";
            var username = emailOpts.Username;
            var password = emailOpts.Password;

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
                NotificationSeverityEnum.Error => "#BA1A1A",
                NotificationSeverityEnum.Warning => "#FFBA08",
                NotificationSeverityEnum.Success => "#12B76A",
                _ => "#005FB8"
            };

            var body = $@"<html>
<head>
<style>
  body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 0; padding: 20px; background-color: #f8f9fa; color: #212529; }}
  .container {{ max-width: 600px; margin: 0 auto; background: #ffffff; border-radius: 8px; border-left: 6px solid {colorHex}; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1); padding: 24px; }}
  .title {{ font-size: 18px; font-weight: 600; margin-bottom: 12px; color: #111827; }}
  .content {{ font-size: 14px; line-height: 1.5; color: #4b5563; margin-bottom: 20px; }}
  .footer {{ font-size: 12px; color: #9ca3af; border-top: 1px solid #e5e7eb; padding-top: 12px; }}
</style>
</head>
<body>
  <div class='container'>
    <div class='title'>{WebUtility.HtmlEncode(notification.Title)}</div>
    <div class='content'>{WebUtility.HtmlEncode(notification.Message)}</div>
    <div class='footer'>Sent by BlazorEnterprise | Automated System Alert</div>
  </div>
</body>
</html>";

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(senderEmail, senderName),
                Subject = $"[{notification.Severity}] {notification.Title}",
                Body = body,
                IsBodyHtml = true
            };
            mailMessage.To.Add(recipientMailbox.Trim());

            await client.SendMailAsync(mailMessage, ct);
            _logger.LogInformation("Successfully sent email notification to {Mailbox} for alert '{Title}'", recipientMailbox, notification.Title);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email notification to {Mailbox} for alert '{Title}'", recipientMailbox, notification.Title);
            return false;
        }
    }
}