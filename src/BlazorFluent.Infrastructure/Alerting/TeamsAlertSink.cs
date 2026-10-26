using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using BlazorFluent.Core.Configuration;
using Serilog.Core;
using Serilog.Events;

namespace BlazorFluent.Infrastructure.Alerting;

/// <summary>
/// Asynchronous Serilog sink that formats and dispatches high-priority alert cards to Microsoft Teams
/// via Incoming Webhooks whenever an Error, Critical, or Fatal log event is emitted.
/// Features non-blocking channel queuing and in-memory rate limiting to protect channels from flood spikes.
/// </summary>
public sealed class TeamsAlertSink : ILogEventSink, IDisposable
{
    private readonly TeamsAlertOptions _options;
    private readonly HttpClient _httpClient;
    private readonly Channel<string> _channel;
    private readonly CancellationTokenSource _cts;
    private readonly Task _workerTask;
    private readonly LogEventLevel _minLevel;
    private readonly ConcurrentQueue<DateTime> _dispatchTimestamps = new();

    public TeamsAlertSink(TeamsAlertOptions options, HttpClient? httpClient = null)
    {
        _options = options;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        _minLevel = ParseMinLevel(options.MinLevel);

        // Bounded channel to prevent memory leaks if webhook is unreachable
        _channel = Channel.CreateBounded<string>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

        _cts = new CancellationTokenSource();
        _workerTask = Task.Run(ProcessQueueAsync);
    }

    public void Emit(LogEvent logEvent)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.WebhookUrl))
        {
            return;
        }

        if (logEvent.Level < _minLevel)
        {
            return;
        }

        if (!CheckRateLimit())
        {
            return;
        }

        try
        {
            var payload = BuildTeamsCardPayload(logEvent);
            _channel.Writer.TryWrite(payload);
        }
        catch
        {
            // Fail silent inside logging sink to prevent crashing the host
        }
    }

    private bool CheckRateLimit()
    {
        var now = DateTime.UtcNow;
        var windowStart = now.AddMinutes(-1);

        while (_dispatchTimestamps.TryPeek(out var oldest) && oldest < windowStart)
        {
            _dispatchTimestamps.TryDequeue(out _);
        }

        if (_dispatchTimestamps.Count >= _options.MaxAlertsPerMinute)
        {
            return false;
        }

        _dispatchTimestamps.Enqueue(now);
        return true;
    }

    private string BuildTeamsCardPayload(LogEvent logEvent)
    {
        var isCritical = logEvent.Level == LogEventLevel.Fatal;
        var themeColor = isCritical ? "A80000" : "D83B01"; // Red for Critical/Fatal, Orange for Error
        var levelLabel = isCritical ? "CRITICAL" : "ERROR";
        var icon = isCritical ? "🚨" : "⚠️";

        var source = GetPropertyString(logEvent, "SourceContext") ?? "Application";
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                          ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                          ?? "Production";

        var renderedMessage = logEvent.RenderMessage();
        if (logEvent.Exception != null)
        {
            renderedMessage += $" (Exception: {logEvent.Exception.GetType().Name}: {logEvent.Exception.Message})";
        }

        // Limit message text length for Teams card readability
        if (renderedMessage.Length > 1500)
        {
            renderedMessage = renderedMessage.Substring(0, 1500) + "... [truncated]";
        }

        var facts = new List<object>
        {
            new { name = "Environment", value = environment },
            new { name = "Severity", value = levelLabel },
            new { name = "Source", value = source }
        };

        var tenantId = GetPropertyString(logEvent, "TenantId");
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            facts.Add(new { name = "Tenant", value = tenantId });
        }

        var requestPath = GetPropertyString(logEvent, "RequestPath");
        if (!string.IsNullOrWhiteSpace(requestPath))
        {
            facts.Add(new { name = "Request Path", value = requestPath });
        }

        var card = new
        {
            @type = "MessageCard",
            @context = "http://schema.org/extensions",
            themeColor = themeColor,
            summary = $"{icon} [{levelLabel}] {source}: {logEvent.MessageTemplate.Text}",
            sections = new[]
            {
                new
                {
                    activityTitle = $"{icon} [{levelLabel}] {source}",
                    activitySubtitle = $"{environment} | {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
                    facts = facts,
                    text = renderedMessage,
                    markdown = true
                }
            }
        };

        return JsonSerializer.Serialize(card);
    }

    private static string? GetPropertyString(LogEvent logEvent, string propertyName)
    {
        if (logEvent.Properties.TryGetValue(propertyName, out var value))
        {
            return value.ToString().Trim('"');
        }
        return null;
    }

    private static LogEventLevel ParseMinLevel(string? level)
    {
        return level?.ToUpperInvariant() switch
        {
            "FATAL" or "CRITICAL" => LogEventLevel.Fatal,
            "WARNING" or "WARN" => LogEventLevel.Warning,
            _ => LogEventLevel.Error
        };
    }

    private async Task ProcessQueueAsync()
    {
        try
        {
            while (await _channel.Reader.WaitToReadAsync(_cts.Token))
            {
                while (_channel.Reader.TryRead(out var payload))
                {
                    try
                    {
                        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
                        await _httpClient.PostAsync(_options.WebhookUrl, content, _cts.Token);
                    }
                    catch
                    {
                        // Webhook dispatch failure is ignored to maintain application stability
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }

    public void Dispose()
    {
        _channel.Writer.TryComplete();
        _cts.Cancel();
        try
        {
            _workerTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Ignore timeout on shutdown
        }
        _cts.Dispose();
        _httpClient.Dispose();
    }
}
