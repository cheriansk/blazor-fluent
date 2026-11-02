namespace BlazorEnterprise.Core.Configuration;

/// <summary>
/// Strongly typed configuration options for external notifications (Teams webhooks and Email).
/// Section: "Notifications"
/// </summary>
public class NotificationOptions
{
    public const string SectionName = "Notifications";

    public TeamsNotificationOptions Teams { get; set; } = new();
    public EmailNotificationOptions Email { get; set; } = new();
}

public class TeamsNotificationOptions
{
    public bool Enabled { get; set; }
    public string DefaultWebhookUrl { get; set; } = string.Empty;
    public Dictionary<string, string> ProjectWebhooks { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class EmailNotificationOptions
{
    public bool Enabled { get; set; }
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public bool UseSsl { get; set; }
    public string SenderEmail { get; set; } = string.Empty;
    public string SenderName { get; set; } = "BlazorEnterprise Notifications";
    public string DefaultMailbox { get; set; } = string.Empty;
    public Dictionary<string, string> ProjectMailboxes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
