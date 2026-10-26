namespace BlazorEnterprise.Core.Configuration;

/// <summary>
/// Strongly typed configuration options for real-time application error and security alerting.
/// Section: "Alerting:Teams"
/// </summary>
public class TeamsAlertOptions
{
    public const string SectionName = "Alerting:Teams";

    /// <summary>
    /// Whether Teams alert dispatching is enabled. Default is true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Microsoft Teams Incoming Webhook endpoint URL.
    /// </summary>
    public string WebhookUrl { get; set; } = string.Empty;

    /// <summary>
    /// Minimum log event level required to trigger an alert card (e.g. "Error", "Critical", "Fatal").
    /// </summary>
    public string MinLevel { get; set; } = "Error";

    /// <summary>
    /// Maximum number of alert cards dispatched per minute to prevent channel flooding. Default is 10.
    /// </summary>
    public int MaxAlertsPerMinute { get; set; } = 10;
}
