using System.Text.Json.Serialization;

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
    public string StagingBatchStrategy { get; set; } = "generic-json";

    [JsonPropertyName("targetEntity")]
    public string TargetEntity { get; set; } = string.Empty;

    [JsonPropertyName("chunkSize")]
    public int ChunkSize { get; set; } = 1000;

    [JsonPropertyName("maxErrorThreshold")]
    public int MaxErrorThreshold { get; set; } = 500;

    [JsonPropertyName("omitNullValues")]
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
    public string Type { get; set; } = "auto"; // auto, excel, delimited, xml, json

    [JsonPropertyName("delimiter")]
    public string Delimiter { get; set; } = ",";

    [JsonPropertyName("hasHeader")]
    public bool HasHeader { get; set; } = true;

    [JsonPropertyName("ignoreMultipleSpaces")]
    public bool IgnoreMultipleSpaces { get; set; }

    [JsonPropertyName("rowElement")]
    public string? RowElement { get; set; }
}

public sealed class SheetTemplateConfig
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "Default";

    [JsonPropertyName("required")]
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
