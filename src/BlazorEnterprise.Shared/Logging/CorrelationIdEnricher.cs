using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace BlazorEnterprise.Shared.Logging;

/// <summary>
/// Enriches Serilog log events with consistent 'CorrelationId', 'CircuitId', and 'SessionId' properties
/// across WebApp circuits, API requests, and background worker jobs.
/// </summary>
public sealed class CorrelationIdEnricher : ILogEventEnricher
{
    public const string CorrelationIdPropertyName = "CorrelationId";
    public const string CircuitIdPropertyName = "CircuitId";
    public const string SessionIdPropertyName = "SessionId";

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        // 1. Resolve CircuitId if present in log event or Activity
        string? circuitId = null;
        if (logEvent.Properties.TryGetValue(CircuitIdPropertyName, out var circuitVal))
        {
            var str = circuitVal.ToString().Trim('"', ' ');
            if (!string.IsNullOrWhiteSpace(str)) circuitId = str;
        }

        // 2. Resolve CorrelationId
        if (!logEvent.Properties.ContainsKey(CorrelationIdPropertyName))
        {
            string? correlationId = null;

            var activity = Activity.Current;
            if (activity != null)
            {
                var tagVal = activity.GetTagItem("blazorenterprise.correlation_id") as string;
                if (!string.IsNullOrWhiteSpace(tagVal))
                {
                    correlationId = tagVal;
                }
                else if (activity.TraceId != default)
                {
                    correlationId = activity.TraceId.ToString();
                }
                else if (!string.IsNullOrWhiteSpace(activity.Id))
                {
                    correlationId = activity.Id;
                }
            }

            if (string.IsNullOrWhiteSpace(correlationId) && logEvent.Properties.TryGetValue("RequestId", out var reqVal))
            {
                var str = reqVal.ToString().Trim('"', ' ');
                if (!string.IsNullOrWhiteSpace(str))
                {
                    correlationId = BlazorEnterprise.Shared.Common.CorrelationIdSanitizer.Sanitize(str.Replace(':', '-'));
                }
            }

            if (string.IsNullOrWhiteSpace(correlationId))
            {
                correlationId = Guid.NewGuid().ToString("N")[..12];
            }

            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(CorrelationIdPropertyName, correlationId));
        }

        // 3. Ensure CircuitId is defaulted if missing so output templates can render cleanly
        if (!logEvent.Properties.ContainsKey(CircuitIdPropertyName))
        {
            if (string.IsNullOrWhiteSpace(circuitId) && logEvent.Properties.TryGetValue("ConnectionId", out var connVal))
            {
                var str = connVal.ToString().Trim('"', ' ');
                if (!string.IsNullOrWhiteSpace(str)) circuitId = BlazorEnterprise.Shared.Common.CorrelationIdSanitizer.Sanitize(str);
            }

            if (string.IsNullOrWhiteSpace(circuitId) && logEvent.Properties.TryGetValue("TransportConnectionId", out var transVal))
            {
                var str = transVal.ToString().Trim('"', ' ');
                if (!string.IsNullOrWhiteSpace(str)) circuitId = BlazorEnterprise.Shared.Common.CorrelationIdSanitizer.Sanitize(str);
            }

            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(CircuitIdPropertyName, circuitId ?? "-"));
        }

        // 4. Ensure SessionId is defaulted if missing
        if (!logEvent.Properties.ContainsKey(SessionIdPropertyName))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(SessionIdPropertyName, "-"));
        }
    }
}
