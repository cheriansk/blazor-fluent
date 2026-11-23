// [NEED-HUMAN-REVIEW]
using System.Text;
using BlazorEnterprise.Shared.Contracts;
using Microsoft.Extensions.Options;

namespace BlazorEnterprise.Api.Middleware;

public sealed class ApiPayloadAuditOptions
{
    public const string SectionName = "ApiPayloadAuditLogging";
    public bool Enabled { get; set; }
    public string LogFilePath { get; set; } = "logs/api-payloads-.log";
}

/// <summary>
/// Logs decrypted API request and response payloads to a separate log file keyed by <c>CorrelationId</c>
/// when <c>ApiPayloadAuditLogging:Enabled</c> is <c>true</c>.
/// </summary>
public sealed class ApiPayloadAuditMiddleware
{
    private static readonly SemaphoreSlim WriteLock = new(1, 1);
    private readonly RequestDelegate _next;
    private readonly IOptionsMonitor<ApiPayloadAuditOptions> _options;
    private readonly ILogger<ApiPayloadAuditMiddleware> _logger;

    public ApiPayloadAuditMiddleware(
        RequestDelegate next,
        IOptionsMonitor<ApiPayloadAuditOptions> options,
        ILogger<ApiPayloadAuditMiddleware> logger)
    {
        _next = next;
        _options = options;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ICorrelationContext correlationContext)
    {
        var opts = _options.CurrentValue;
        if (!opts.Enabled)
        {
            await _next(context);
            return;
        }

        context.Request.EnableBuffering();
        string requestBody;
        using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true))
        {
            requestBody = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;
        }

        var originalBody = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await _next(context);
        }
        finally
        {
            buffer.Position = 0;
            var responseBody = await new StreamReader(buffer, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true).ReadToEndAsync();
            buffer.Position = 0;
            await buffer.CopyToAsync(originalBody);
            context.Response.Body = originalBody;

            await WriteAuditEntryAsync(opts.LogFilePath, correlationContext.CorrelationId, context.Request.Method, context.Request.Path, context.Response.StatusCode, requestBody, responseBody);
        }
    }

    private async Task WriteAuditEntryAsync(
        string configuredPath,
        string correlationId,
        string method,
        PathString path,
        int statusCode,
        string requestBody,
        string responseBody)
    {
        try
        {
            var dateSuffix = DateTime.UtcNow.ToString("yyyyMMdd");
            var filePath = configuredPath.EndsWith("-.log", StringComparison.OrdinalIgnoreCase)
                ? configuredPath[..^4] + dateSuffix + ".log"
                : configuredPath;

            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var entry = $"{DateTime.UtcNow:O} | CorrelationId={correlationId} | {method} {path} | Status={statusCode}{Environment.NewLine}" +
                        $"Request: {requestBody}{Environment.NewLine}" +
                        $"Response: {responseBody}{Environment.NewLine}" +
                        $"---{Environment.NewLine}";

            await WriteLock.WaitAsync();
            try
            {
                await File.AppendAllTextAsync(filePath, entry, Encoding.UTF8);
            }
            finally
            {
                WriteLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write API payload audit entry for CorrelationId {CorrelationId}.", correlationId);
        }
    }
}
