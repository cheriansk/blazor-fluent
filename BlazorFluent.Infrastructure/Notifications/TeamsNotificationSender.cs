using System.Text;
using System.Text.Json;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Infrastructure.Notifications;

public class TeamsNotificationSender : ITeamsNotificationSender
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TeamsNotificationSender> _logger;

    public TeamsNotificationSender(
        IHttpClientFactory httpClientFactory,
        ILogger<TeamsNotificationSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<bool> SendTeamsNotificationAsync(NotificationEntity notification, string webhookUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            _logger.LogWarning("Teams notification skipped: Webhook URL is empty.");
            return false;
        }

        try
        {
            var themeColor = notification.Severity switch
            {
                NotificationSeverity.Success => "107C41", // Green
                NotificationSeverity.Warning => "D83B01", // Orange
                NotificationSeverity.Error => "A80000",   // Red
                _ => "0076D7"                            // Blue (Info)
            };

            var actions = new List<object>();
            if (!string.IsNullOrWhiteSpace(notification.LinkUrl))
            {
                actions.Add(new
                {
                    @type = "OpenUri",
                    name = "Open Resource",
                    targets = new[]
                    {
                        new { os = "default", uri = notification.LinkUrl }
                    }
                });
            }

            var card = new
            {
                @type = "MessageCard",
                @context = "http://schema.org/extensions",
                themeColor = themeColor,
                summary = notification.Title,
                sections = new[]
                {
                    new
                    {
                        activityTitle = $"[{notification.Severity.ToString().ToUpperInvariant()}] {notification.Title}",
                        activitySubtitle = $"Tenant: {notification.TenantId} | {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
                        text = notification.Message,
                        markdown = true
                    }
                },
                potentialAction = actions.Count > 0 ? actions : null
            };

            var json = JsonSerializer.Serialize(card);
            using var client = _httpClientFactory.CreateClient("TeamsWebhook");
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(webhookUrl, content, ct);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Teams notification successfully dispatched for '{Title}'", notification.Title);
                return true;
            }

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Teams webhook returned non-success status code {StatusCode}: {ResponseBody}", response.StatusCode, responseBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dispatch Microsoft Teams notification to webhook: {WebhookUrl}", webhookUrl);
            return false;
        }
    }
}
