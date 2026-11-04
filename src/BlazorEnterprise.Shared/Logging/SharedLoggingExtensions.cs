using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Configuration;
using Serilog.Enrichers.Span;
using Serilog.Events;
using Serilog.Filters;

namespace BlazorEnterprise.Shared.Logging;

/// <summary>
/// Unified Serilog setup shared across Client WebApp and Cloud ProcessAPI.
/// Enforces UTC timestamps, process enrichers, Span/Activity correlation, cross-file CorrelationId,
/// and configurable log file splitting via 'Logging:SplitLogFiles'.
/// </summary>
public static class SharedLoggingExtensions
{
    public static IHostBuilder UseSharedSerilog(
        this IHostBuilder hostBuilder, 
        string applicationName,
        Action<LoggerSinkConfiguration, IConfiguration>? configureExtraSinks = null)
    {
        return hostBuilder.UseSerilog((context, services, configuration) =>
        {
            var useUtc = !bool.TryParse(context.Configuration["DateTimeSettings:UseUtc"], out var parsedUtc) || parsedUtc;
            var timestampFormat = useUtc ? "yyyy-MM-dd HH:mm:ss 'UTC'" : "yyyy-MM-dd HH:mm:ss";
            var logOutputTemplate = $"[{{Timestamp:{timestampFormat}}}] [{{Level:u3}}] [{applicationName}] [Circuit:{{CircuitId}}] [Session:{{SessionId}}] [CorrId:{{CorrelationId}}] {{Message:lj}} {{Properties:j}}{{NewLine}}{{Exception}}";

            var rootLogsDirectory = ResolveRootLogsDirectory(context.HostingEnvironment.ContentRootPath);
            Directory.CreateDirectory(rootLogsDirectory);

            var splitLogFiles = !bool.TryParse(context.Configuration["Logging:SplitLogFiles"], out var parsedSplit) || parsedSplit;

            var auditFilter = Matching.FromSource("BlazorEnterprise.Persistence.Services.AuditService");
            var tenantContextFilter = Matching.FromSource("BlazorEnterprise.Core.Contracts.TenantContext");
            var dbCommandFilter = Matching.FromSource("BlazorEnterprise.Persistence.Interceptors.DbCommandLoggingInterceptor");

            var errorLogPath = Path.Combine(rootLogsDirectory, $"{applicationName.ToLowerInvariant()}-errors-.log");
            var auditLogPath = Path.Combine(rootLogsDirectory, $"{applicationName.ToLowerInvariant()}-audit-.log");
            var dbLogPath = Path.Combine(rootLogsDirectory, $"{applicationName.ToLowerInvariant()}-db-.log");
            var mainLogPath = Path.Combine(rootLogsDirectory, $"{applicationName.ToLowerInvariant()}-.log");

            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", applicationName)
                .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
                .Enrich.WithSpan()
                .Enrich.With<CorrelationIdEnricher>()
                .WriteTo.StrictSeverity(wt =>
                {
                    // 1. Console Output for developer visibility (excludes routine SQL query chatter; still shows SQL errors)
                    wt.Logger(lc => lc
                        .Filter.ByExcluding(le => dbCommandFilter(le) && le.Level < LogEventLevel.Error)
                        .WriteTo.Console(outputTemplate: logOutputTemplate));

                    if (splitLogFiles)
                    {
                        // 2a. Dedicated Error/Fatal/Critical Log Sink (Always captures issues including SQL exceptions across 60 days)
                        wt.File(
                            path: errorLogPath,
                            restrictedToMinimumLevel: LogEventLevel.Error,
                            rollingInterval: RollingInterval.Day,
                            retainedFileCountLimit: 60,
                            outputTemplate: logOutputTemplate);

                        // 3a. Dedicated Audit Log Sink (Captures all security, user activity, and query bypass audit events across 30 days)
                        wt.Logger(lc => lc
                            .Filter.ByIncludingOnly(auditFilter)
                            .WriteTo.File(
                                path: auditLogPath,
                                rollingInterval: RollingInterval.Day,
                                retainedFileCountLimit: 30,
                                outputTemplate: logOutputTemplate));

                        // 4a. Dedicated Database Operations Log Sink (Captures all SQL queries, parameters, execution duration, and SQL exceptions)
                        wt.Logger(lc => lc
                            .Filter.ByIncludingOnly(dbCommandFilter)
                            .WriteTo.File(
                                path: dbLogPath,
                                rollingInterval: RollingInterval.Day,
                                retainedFileCountLimit: 14,
                                outputTemplate: logOutputTemplate));

                        // 5a. Primary Application Log Sink (Excludes audit messages, DB query logs, and routine tenant-init noise so it stays clean and readable)
                        wt.Logger(lc => lc
                            .Filter.ByExcluding(auditFilter)
                            .Filter.ByExcluding(tenantContextFilter)
                            .Filter.ByExcluding(dbCommandFilter)
                            .WriteTo.File(
                                path: mainLogPath,
                                rollingInterval: RollingInterval.Day,
                                retainedFileCountLimit: 30,
                                outputTemplate: logOutputTemplate));
                    }
                    else
                    {
                        // Single unified log file mode (All log events in one file)
                        wt.File(
                            path: mainLogPath,
                            rollingInterval: RollingInterval.Day,
                            retainedFileCountLimit: 30,
                            outputTemplate: logOutputTemplate);
                    }

                    // 5. Allow calling hosts to configure extra sinks (e.g. TeamsAlert)
                    configureExtraSinks?.Invoke(wt, context.Configuration);
                });
        });
    }

    private static string ResolveRootLogsDirectory(string contentRootPath)
    {
        var dir = new DirectoryInfo(contentRootPath);
        while (dir != null && dir.Parent != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "BlazorEnterprise.slnx")) ||
                Directory.Exists(Path.Combine(dir.FullName, ".git")))
            {
                return Path.Combine(dir.FullName, "logs");
            }
            dir = dir.Parent;
        }

        return Path.Combine(contentRootPath, "logs");
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
