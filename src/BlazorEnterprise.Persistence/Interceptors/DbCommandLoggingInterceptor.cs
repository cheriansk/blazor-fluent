using System.Collections;
using System.Data.Common;
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
/// Correlated via Serilog's CorrelationIdEnricher ([CorrId:...]).
/// </summary>
public sealed class DbCommandLoggingInterceptor : DbCommandInterceptor
{
    private readonly ILogger<DbCommandLoggingInterceptor> _logger;
    private readonly bool _enableDbLogging;
    private readonly bool _logDbParameters;

    public DbCommandLoggingInterceptor(
        ILogger<DbCommandLoggingInterceptor> logger,
        IOptions<DatabaseLoggingOptions>? options = null)
    {
        _logger = logger;
        var opts = options?.Value ?? new DatabaseLoggingOptions();
        _enableDbLogging = opts.EnableDbLogging;
        _logDbParameters = opts.LogDbParameters;
    }

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

    private void LogCommandExecuted(DbCommand command, CommandExecutedEventData eventData)
    {
        if (!_enableDbLogging)
        {
            return;
        }

        var durationMs = eventData.Duration.TotalMilliseconds;
        var parameters = _logDbParameters ? FormatParameters(command.Parameters) : "[Redacted by Configuration]";
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
        var durationMs = eventData.Duration.TotalMilliseconds;
        var parameters = _logDbParameters ? FormatParameters(command.Parameters) : "[Redacted by Configuration]";
        var sql = command.CommandText?.Trim() ?? string.Empty;

        _logger.LogError(
            eventData.Exception,
            "SQL FAILED ({DurationMs:0.00} ms) | Params: [{Parameters}]{NewLine}{Sql}",
            durationMs,
            parameters,
            Environment.NewLine,
            sql);
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
            return $"'{dt.ToString("O", CultureInfo.InvariantCulture)}'";
        }

        if (value is DateTimeOffset dto)
        {
            return $"'{dto.ToString("O", CultureInfo.InvariantCulture)}'";
        }

        if (value is bool b)
        {
            return b ? "true" : "false";
        }

        if (value is byte[] bytes)
        {
            return $"<byte[{bytes.Length}]>";
        }

        if (value is IEnumerable enumerable and not string)
        {
            var items = new List<string>();
            var count = 0;
            foreach (var item in enumerable)
            {
                if (count >= 25)
                {
                    items.Add("...");
                    break;
                }
                items.Add(FormatParameterValue(item));
                count++;
            }
            return "[" + string.Join(", ", items) + "]";
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "NULL";
    }
}
