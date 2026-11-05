using System.Collections;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using BlazorEnterprise.Core.Configuration;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlazorEnterprise.Persistence.Interceptors;

/// <summary>
/// EF Core <see cref="DbCommandInterceptor"/> that logs executed SQL queries, bound parameters,
/// execution duration, and any SQL exceptions to the dedicated DB operations log sink.
/// Correlated via Serilog's CorrelationIdEnricher ([CorrId:...]) and optional SQL comment tagging (/* CorrId:... */).
/// Dynamic runtime updates supported via <see cref="IOptionsMonitor{DatabaseLoggingOptions}"/>.
/// </summary>
public sealed class DbCommandLoggingInterceptor : DbCommandInterceptor
{
    private readonly ILogger<DbCommandLoggingInterceptor> _logger;
    private readonly IOptionsMonitor<DatabaseLoggingOptions> _optionsMonitor;

    public DbCommandLoggingInterceptor(
        ILogger<DbCommandLoggingInterceptor> logger,
        IOptionsMonitor<DatabaseLoggingOptions> optionsMonitor)
    {
        _logger = logger;
        _optionsMonitor = optionsMonitor;
    }

    // --- Command Executing (SQL Comment Tagging) ---

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        TagCommandText(command);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        TagCommandText(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        TagCommandText(command);
        return base.NonQueryExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        TagCommandText(command);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result)
    {
        TagCommandText(command);
        return base.ScalarExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        TagCommandText(command);
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    // --- Command Executed Logging ---

    public override DbDataReader ReaderExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result)
    {
        LogCommandExecuted(command, eventData);
        return base.ReaderExecuted(command, eventData, result);
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        LogCommandExecuted(command, eventData);
        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override int NonQueryExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result)
    {
        LogCommandExecuted(command, eventData);
        return base.NonQueryExecuted(command, eventData, result);
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        LogCommandExecuted(command, eventData);
        return base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override object? ScalarExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result)
    {
        LogCommandExecuted(command, eventData);
        return base.ScalarExecuted(command, eventData, result);
    }

    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        LogCommandExecuted(command, eventData);
        return base.ScalarExecutedAsync(command, eventData, result, cancellationToken);
    }

    // --- Command Failed Logging ---

    public override void CommandFailed(
        DbCommand command,
        CommandErrorEventData eventData)
    {
        LogCommandFailed(command, eventData);
        base.CommandFailed(command, eventData);
    }

    public override Task CommandFailedAsync(
        DbCommand command,
        CommandErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        LogCommandFailed(command, eventData);
        return base.CommandFailedAsync(command, eventData, cancellationToken);
    }

    private void TagCommandText(DbCommand command)
    {
        var opts = _optionsMonitor.CurrentValue;
        if (!opts.TagSqlWithCorrelationId || string.IsNullOrWhiteSpace(command.CommandText))
        {
            return;
        }

        if (command.CommandText.StartsWith("/* CorrId:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var correlationId = ResolveCurrentCorrelationId();
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            command.CommandText = $"/* CorrId: {correlationId} */{Environment.NewLine}{command.CommandText}";
        }
    }

    private void LogCommandExecuted(DbCommand command, CommandExecutedEventData eventData)
    {
        var opts = _optionsMonitor.CurrentValue;
        if (opts.Mode != DatabaseLoggingMode.All)
        {
            return;
        }

        var durationMs = eventData.Duration.TotalMilliseconds;
        var parameters = opts.LogDbParameters ? FormatParameters(command.Parameters) : "[Redacted by Configuration]";
        var sql = command.CommandText?.Trim() ?? string.Empty;

        _logger.LogInformation(
            "SQL Executed ({DurationMs:0.00} ms) | Params: [{Parameters}]{NewLine}{Sql}",
            durationMs,
            parameters,
            Environment.NewLine,
            sql);
    }

    private void LogCommandFailed(DbCommand command, CommandErrorEventData eventData)
    {
        var opts = _optionsMonitor.CurrentValue;
        if (opts.Mode == DatabaseLoggingMode.None)
        {
            return;
        }

        var durationMs = eventData.Duration.TotalMilliseconds;
        var parameters = opts.LogDbParameters ? FormatParameters(command.Parameters) : "[Redacted by Configuration]";
        var sql = command.CommandText?.Trim() ?? string.Empty;

        _logger.LogError(
            eventData.Exception,
            "SQL FAILED ({DurationMs:0.00} ms) | Params: [{Parameters}]{NewLine}{Sql}",
            durationMs,
            parameters,
            Environment.NewLine,
            sql);
    }

    private static string? ResolveCurrentCorrelationId()
    {
        var activity = Activity.Current;
        if (activity != null)
        {
            var tagVal = activity.GetTagItem("blazorenterprise.correlation_id") as string;
            if (!string.IsNullOrWhiteSpace(tagVal)) return tagVal;
            if (activity.TraceId != default) return activity.TraceId.ToString();
            if (!string.IsNullOrWhiteSpace(activity.Id)) return activity.Id;
        }
        return null;
    }

    private static string FormatParameters(DbParameterCollection? parameters)
    {
        if (parameters == null || parameters.Count == 0)
        {
            return "none";
        }

        var sb = new StringBuilder();
        for (var i = 0; i < parameters.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            var param = parameters[i];
            var name = string.IsNullOrWhiteSpace(param.ParameterName) ? $"@p{i}" : param.ParameterName;
            if (!name.StartsWith('@') && !name.StartsWith('$'))
            {
                name = "@" + name;
            }

            if (IsSensitiveParameterName(name))
            {
                sb.Append(name).Append("='***REDACTED***' (").Append(param.DbType).Append(')');
                continue;
            }

            var formattedValue = FormatParameterValue(param.Value);
            sb.Append(name).Append('=').Append(formattedValue).Append(" (").Append(param.DbType).Append(')');
        }

        return sb.ToString();
    }

    private static bool IsSensitiveParameterName(string name)
    {
        return name.Contains("password", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("masterkey", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("signingkey", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatParameterValue(object? value)
    {
        if (value is null or DBNull)
        {
            return "NULL";
        }

        if (value is string str)
        {
            var truncated = str.Length > 500 ? string.Concat(str.AsSpan(0, 500), "...(truncated)") : str;
            return $"'{truncated}'";
        }

        if (value is Guid guid)
        {
            return $"'{guid}'";
        }

        if (value is DateTime dt)
        {
            return $"'{dt:yyyy-MM-dd HH:mm:ss.fff}'";
        }

        if (value is DateTimeOffset dto)
        {
            return $"'{dto:yyyy-MM-dd HH:mm:ss.fff zzz}'";
        }

        if (value is bool b)
        {
            return b ? "TRUE" : "FALSE";
        }

        if (value is byte[] bytes)
        {
            return $"byte[{bytes.Length}]";
        }

        if (value is IEnumerable enumerable and not string)
        {
            var count = 0;
            foreach (var _ in enumerable) count++;
            return $"IEnumerable[{count}]";
        }

        if (value is IFormattable formattable)
        {
            return formattable.ToString(null, CultureInfo.InvariantCulture);
        }

        return value.ToString() ?? "NULL";
    }
}
