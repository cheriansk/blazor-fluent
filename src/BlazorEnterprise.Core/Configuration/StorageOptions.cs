namespace BlazorEnterprise.Core.Configuration;

/// <summary>
/// Strongly typed configuration options for tenant blob storage.
/// Section: "Storage"
/// </summary>
public class StorageOptions
{
    public const string SectionName = "Storage";

    public string? AzureBlobConnectionString { get; set; }
    public string ContainerName { get; set; } = "imports";
    public string? LocalRootPath { get; set; }
}