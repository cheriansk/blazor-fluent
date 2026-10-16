using System.Collections.Concurrent;
using System.Text.Json;
using BlazorFluent.Shared.Common;
using BlazorFluent.Shared.Contracts.Engine.Transforms;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Jobs.FileImports.Configuration;

/// <summary>
/// Repository loading and caching declarative FileTemplateConfig specifications from config/file-templates/*.json.
/// </summary>
public interface IFileTemplateConfigRepository
{
    Task<FileTemplateConfig?> GetConfigAsync(string fileType, CancellationToken ct = default);
    FileTemplateConfig? GetTemplate(string fileType);
}

public sealed class JsonFileTemplateConfigRepository : IFileTemplateConfigRepository
{
    private readonly ILogger<JsonFileTemplateConfigRepository> _logger;
    private readonly string _templatesDirectory;
    private readonly ConcurrentDictionary<string, (FileTemplateConfig Config, DateTime ExpiresAtUtc)> _cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    public FileTemplateConfig? GetTemplate(string fileType)
    {
        return GetConfigAsync(fileType).GetAwaiter().GetResult();
    }

    public JsonFileTemplateConfigRepository(
        ILogger<JsonFileTemplateConfigRepository> logger,
        string? templatesDirectory = null)
    {
        _logger = logger;
        _templatesDirectory = templatesDirectory ?? Path.Combine(AppContext.BaseDirectory, "config", "file-templates");
    }

    public async Task<FileTemplateConfig?> GetConfigAsync(string fileType, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(fileType))
            return null;

        if (_cache.TryGetValue(fileType, out var cached) && cached.ExpiresAtUtc > DateTime.UtcNow)
        {
            return cached.Config;
        }

        // 1. Check disk locations (AppContext.BaseDirectory and current working directory)
        var candidatePaths = new[]
        {
            Path.Combine(_templatesDirectory, $"{fileType}.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "config", "file-templates", $"{fileType}.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "config", "file-templates", $"{fileType}.json")
        };

        foreach (var filePath in candidatePaths)
        {
            if (File.Exists(filePath))
            {
                try
                {
                    await using var stream = File.OpenRead(filePath);
                    var config = await JsonSerializer.DeserializeAsync<FileTemplateConfig>(stream, JsonDefaults.StandardOptions, ct);
                    if (config != null)
                    {
                        if (string.IsNullOrWhiteSpace(config.FileType))
                            config.FileType = fileType;

                        _cache[fileType] = (config, DateTime.UtcNow.Add(CacheDuration));
                        _logger.LogInformation("Loaded and cached FileTemplateConfig for '{FileType}' from '{Path}'.", fileType, filePath);
                        return config;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to parse FileTemplateConfig JSON at '{Path}'.", filePath);
                }
            }
        }

        // 2. Fallback to built-in default template for standard TaskCsv / TaskExcel
        var builtIn = CreateBuiltInTemplate(fileType);
        if (builtIn != null)
        {
            _cache[fileType] = (builtIn, DateTime.UtcNow.Add(CacheDuration));
            return builtIn;
        }

        return null;
    }

    private static FileTemplateConfig? CreateBuiltInTemplate(string fileType)
    {
        if (string.Equals(fileType, "TaskCsv", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileType, "TaskExcel", StringComparison.OrdinalIgnoreCase))
        {
            var isExcel = string.Equals(fileType, "TaskExcel", StringComparison.OrdinalIgnoreCase);
            return new FileTemplateConfig
            {
                FileType = fileType,
                StagingBatchStrategy = "generic-json",
                TargetEntity = "TaskEntity",
                ChunkSize = 1000,
                MaxErrorThreshold = 500,
                OmitNullValues = true,
                Format = new FileFormatConfig
                {
                    Type = isExcel ? "excel" : "delimited",
                    Delimiter = ",",
                    HasHeader = true
                },
                Sheets =
                [
                    new SheetTemplateConfig
                    {
                        Name = "Tasks",
                        Required = isExcel
                    }
                ],
                Columns =
                [
                    new ColumnTemplateConfig { Name = "Title", Alias = "c1", DataType = "string", Required = true, MaxLength = 200 },
                    new ColumnTemplateConfig { Name = "Description", Alias = "c2", DataType = "string", Required = false, MaxLength = 10000 },
                    new ColumnTemplateConfig { Name = "Priority", Alias = "c3", DataType = "string", Required = false, AllowedValues = ["Low", "Medium", "High", "Critical", "Urgent"] },
                    new ColumnTemplateConfig { Name = "Status", Alias = "c4", DataType = "string", Required = false, AllowedValues = ["Open", "InProgress", "InReview", "Blocked", "Closed"] },
                    new ColumnTemplateConfig { Name = "DueDate", Alias = "c5", DataType = "date", Required = false },
                    new ColumnTemplateConfig { Name = "AssigneeEmails", Alias = "c6", DataType = "string", Required = false, MaxLength = 2000 },
                    new ColumnTemplateConfig { Name = "Labels", Alias = "c7", DataType = "string", Required = false, MaxLength = 500 }
                ]
            };
        }

        return null;
    }
}
