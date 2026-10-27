using System.Security;
using System.Security.Cryptography;
using Serilog.Core;
using Serilog.Events;

namespace BlazorEnterprise.Shared.Logging;

/// <summary>
/// Wraps any Serilog <see cref="ILogEventSink"/> to enforce strict enterprise severity rules:
/// 1. Any log event carrying an <see cref="Exception"/> (including framework warnings like Blazor's RemoteRenderer EventId 100)
///    is promoted to at least <see cref="LogEventLevel.Error"/>, or <see cref="LogEventLevel.Fatal"/> if it is a security/blocker exception.
/// 2. Any security violation, tamper detection, or dead blocker message is promoted to <see cref="LogEventLevel.Fatal"/>.
/// </summary>
public sealed class StrictExceptionSeveritySink : ILogEventSink, IDisposable
{
    private readonly ILogEventSink _innerSink;

    public StrictExceptionSeveritySink(ILogEventSink innerSink)
    {
        _innerSink = innerSink ?? throw new ArgumentNullException(nameof(innerSink));
    }

    public void Emit(LogEvent logEvent)
    {
        if (logEvent is null)
        {
            return;
        }

        var targetLevel = DetermineStrictLevel(logEvent);
        if (targetLevel > logEvent.Level)
        {
            var properties = new List<LogEventProperty>(logEvent.Properties.Count);
            foreach (var kvp in logEvent.Properties)
            {
                properties.Add(new LogEventProperty(kvp.Key, kvp.Value));
            }

            var promotedEvent = new LogEvent(
                logEvent.Timestamp,
                targetLevel,
                logEvent.Exception,
                logEvent.MessageTemplate,
                properties,
                logEvent.TraceId ?? default,
                logEvent.SpanId ?? default);

            _innerSink.Emit(promotedEvent);
            return;
        }

        _innerSink.Emit(logEvent);
    }

    private static LogEventLevel DetermineStrictLevel(LogEvent logEvent)
    {
        if (logEvent.Level == LogEventLevel.Fatal)
        {
            return LogEventLevel.Fatal;
        }

        var templateText = logEvent.MessageTemplate.Text;
        if (!string.IsNullOrEmpty(templateText) &&
            (templateText.Contains("SECURITY VIOLATION", StringComparison.OrdinalIgnoreCase) ||
             templateText.Contains("ZERO-TRUST", StringComparison.OrdinalIgnoreCase) ||
             templateText.Contains("SECURITY ALERT", StringComparison.OrdinalIgnoreCase) ||
             templateText.Contains("TAMPER", StringComparison.OrdinalIgnoreCase) ||
             templateText.Contains("DEAD BLOCKER", StringComparison.OrdinalIgnoreCase) ||
             templateText.Contains("FATAL CONFIGURATION BLOCKER", StringComparison.OrdinalIgnoreCase)))
        {
            return LogEventLevel.Fatal;
        }

        if (logEvent.Exception is not null)
        {
            if (IsFatalException(logEvent.Exception))
            {
                return LogEventLevel.Fatal;
            }

            return logEvent.Level < LogEventLevel.Error ? LogEventLevel.Error : logEvent.Level;
        }

        // Check for Blazor RemoteRenderer ExceptionRenderingComponent (EventId 100) even if Exception property was formatted into message
        if (logEvent.Properties.TryGetValue("SourceContext", out var sourceContext) &&
            sourceContext.ToString().Contains("RemoteRenderer", StringComparison.OrdinalIgnoreCase) &&
            logEvent.Level == LogEventLevel.Warning)
        {
            return LogEventLevel.Error;
        }

        return logEvent.Level;
    }

    private static bool IsFatalException(Exception ex)
    {
        var current = ex;
        while (current is not null)
        {
            if (current is SecurityException or UnauthorizedAccessException or CryptographicException or UriFormatException)
            {
                return true;
            }

            if (current is InvalidOperationException &&
                (current.Message.Contains("Security", StringComparison.OrdinalIgnoreCase) ||
                 current.Message.Contains("Zero-Trust", StringComparison.OrdinalIgnoreCase) ||
                 current.Message.Contains("Blocker", StringComparison.OrdinalIgnoreCase) ||
                 current.Message.Contains("License", StringComparison.OrdinalIgnoreCase) ||
                 current.Message.Contains("KeyVault", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            current = current.InnerException;
        }

        return false;
    }

    public void Dispose()
    {
        if (_innerSink is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
