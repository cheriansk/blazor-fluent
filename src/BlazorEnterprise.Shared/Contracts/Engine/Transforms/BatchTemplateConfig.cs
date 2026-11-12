using System.Text.Json.Serialization;
using BlazorEnterprise.Shared.Attributes;

namespace BlazorEnterprise.Shared.Contracts.Engine.Transforms;

/// <summary>
/// Declarative file template configuration for the Hybrid Batch Processing Framework.
/// Defines local validation/staging rules, column alias mappings, chunk sizes, and dependencies.
/// </summary>
public sealed class FileTemplateConfig
{
    [JsonPropertyName("fileType")]
    public string FileType { get; set; } = string.Empty;

    [JsonPropertyName("stagingBatchStrategy")]
    [AllowHardcodedDefault("Batch template default staging strategy")]
    public string StagingBatchStrategy { get; set; } = "generic-json";

    [JsonPropertyName("targetEntity")]
    public string TargetEntity { get; set; } = string.Empty;

    [JsonPropertyName("chunkSize")]
    [AllowHardcodedDefault("Batch processing default chunk size")]
    public int ChunkSize { get; set; } = 1000;

    [JsonPropertyName("maxErrorThreshold")]
    [AllowHardcodedDefault("Batch processing default error threshold")]
    public int MaxErrorThreshold { get; set; } = 500;

    [JsonPropertyName("omitNullValues")]
    [AllowHardcodedDefault("Batch processing default to omit null values")]
    public bool OmitNullValues { get; set; } = true;

    [JsonPropertyName("encryptAtRest")]
    public bool? EncryptAtRest { get; set; }

    [JsonPropertyName("dependsOn")]
    public List<string> DependsOn { get; set; } = [];

    [JsonPropertyName("format")]
    public FileFormatConfig Format { get; set; } = new();

    [JsonPropertyName("sheets")]
    public List<SheetTemplateConfig> Sheets { get; set; } = [];

    [JsonPropertyName("columns")]
    public List<ColumnTemplateConfig> Columns { get; set; } = [];

    [JsonPropertyName("uniqueKeys")]
    public List<string> UniqueKeys { get; set; } = [];
}

public sealed class FileFormatConfig
{
    [JsonPropertyName("type")]
    [AllowHardcodedDefault("Batch format default type")]
    public string Type { get; set; } = "auto"; // auto, excel, delimited, xml, json

    [JsonPropertyName("delimiter")]
    [AllowHardcodedDefault("Batch format default delimiter")]
    public string Delimiter { get; set; } = ",";

    [JsonPropertyName("hasHeader")]
    [AllowHardcodedDefault("Batch format default hasHeader")]
    public bool HasHeader { get; set; } = true;

    [JsonPropertyName("ignoreMultipleSpaces")]
    public bool IgnoreMultipleSpaces { get; set; }

    [JsonPropertyName("rowElement")]
    public string? RowElement { get; set; }
}

public sealed class SheetTemplateConfig
{
    [JsonPropertyName("name")]
    [AllowHardcodedDefault("Default sheet name")]
    public string Name { get; set; } = "Default";

    [JsonPropertyName("required")]
    [AllowHardcodedDefault("Default required flag")]
    public bool Required { get; set; } = true;

    [JsonPropertyName("columns")]
    public List<ColumnTemplateConfig> Columns { get; set; } = [];

    [JsonPropertyName("uniqueKeys")]
    public List<string> UniqueKeys { get; set; } = [];
}

public sealed class ColumnTemplateConfig
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("alias")]
    public string? Alias { get; set; }

    [JsonPropertyName("dataType")]
    [AllowHardcodedDefault("JSON column template defaults to string type")]
    public string DataType { get; set; } = "string"; // string, integer, decimal, date, boolean, guid

    [JsonPropertyName("required")]
    public bool Required { get; set; }

    [JsonPropertyName("maxLength")]
    public int? MaxLength { get; set; }

    [JsonPropertyName("allowedValues")]
    public List<string>? AllowedValues { get; set; }

    [JsonPropertyName("regexPattern")]
    public string? RegexPattern { get; set; }

    [JsonPropertyName("defaultValue")]
    public string? DefaultValue { get; set; }
}
