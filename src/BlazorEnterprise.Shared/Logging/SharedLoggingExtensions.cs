using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Configuration;
using Serilog.Enrichers.Span;
using Serilog.Events;

namespace BlazorEnterprise.Shared.Logging;

/// <summary>
/// Unified Serilog setup shared across Client WebApp and Cloud ProcessAPI.
/// Enforces UTC timestamps, process enrichers, Span/Activity correlation, and JSON structured sinks.
/// </summary>
public static class SharedLoggingExtensions
{
    public static IHostBuilder UseSharedSerilog(
        this IHostBuilder hostBuilder, 
        string applicationName)
    {
        return hostBuilder.UseSerilog((context, services, configuration) =>
        {
            var useUtc = !bool.TryParse(context.Configuration["DateTimeSettings:UseUtc"], out var parsedUtc) || parsedUtc;
            var timestampFormat = useUtc ? "yyyy-MM-dd HH:mm:ss 'UTC'" : "yyyy-MM-dd HH:mm:ss";
            var logOutputTemplate = $"[{{Timestamp:{timestampFormat}}}] [{{Level:u3}}] [{applicationName}] {{Message:lj}} {{Properties:j}}{{NewLine}}{{Exception}}";

            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", applicationName)
                .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
                .Enrich.WithSpan()
                .WriteTo.StrictSeverity(wt =>
                {
                    wt.Console(outputTemplate: logOutputTemplate);
                    wt.File(
                        path: $"logs/{applicationName.ToLowerInvariant()}-.log",
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 30,
                        outputTemplate: logOutputTemplate);
                });
        });
    }

    /// <summary>
    /// Wraps inner Serilog sinks in <see cref="StrictExceptionSeveritySink"/> so all exceptions are logged at Error/Fatal
    /// and all security violations or dead blockers are promoted to Fatal ([FTL]).
    /// </summary>
    public static LoggerConfiguration StrictSeverity(
        this LoggerSinkConfiguration sinkConfiguration,
        Action<LoggerSinkConfiguration> configureWrappedSinks)
    {
        return LoggerSinkConfiguration.Wrap(
            sinkConfiguration,
            innerSink => new StrictExceptionSeveritySink(innerSink),
            configureWrappedSinks,
            LogEventLevel.Verbose,
            null);
    }
}
