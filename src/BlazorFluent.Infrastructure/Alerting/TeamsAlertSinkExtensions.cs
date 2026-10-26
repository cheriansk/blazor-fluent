using BlazorFluent.Core.Configuration;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Configuration;
using Serilog.Events;

namespace BlazorFluent.Infrastructure.Alerting;

public static class TeamsAlertSinkExtensions
{
    /// <summary>
    /// Writes log events with level Error, Critical, and Fatal to Microsoft Teams via Incoming Webhooks.
    /// Reads options from the "Alerting:Teams" configuration section.
    /// </summary>
    public static LoggerConfiguration TeamsAlert(
        this LoggerSinkConfiguration sinkConfiguration,
        IConfiguration configuration,
        LogEventLevel restrictedToMinimumLevel = LogEventLevel.Error)
    {
        if (sinkConfiguration == null) throw new ArgumentNullException(nameof(sinkConfiguration));
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));

        var options = new TeamsAlertOptions();
        configuration.GetSection(TeamsAlertOptions.SectionName).Bind(options);

        // Fallback to Notifications:Teams:DefaultWebhookUrl if Alerting:Teams:WebhookUrl is omitted
        if (string.IsNullOrWhiteSpace(options.WebhookUrl))
        {
            var fallback = configuration["Notifications:Teams:DefaultWebhookUrl"];
            if (!string.IsNullOrWhiteSpace(fallback))
            {
                options.WebhookUrl = fallback;
            }
        }

        if (!options.Enabled || string.IsNullOrWhiteSpace(options.WebhookUrl))
        {
            // Webhook not configured; sink is a no-op
            return sinkConfiguration.Sink(new TeamsAlertSink(options), restrictedToMinimumLevel);
        }

        return sinkConfiguration.Sink(new TeamsAlertSink(options), restrictedToMinimumLevel);
    }
}
