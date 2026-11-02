namespace BlazorEnterprise.Core.Configuration;

/// <summary>
/// Strongly typed configuration options for proprietary storage encryption.
/// Section: "ProprietaryStorage"
/// </summary>
public class ProprietaryStorageOptions
{
    public const string SectionName = "ProprietaryStorage";

    public bool EncryptProcessedDataAtRest { get; set; }
    public string VaultMasterKey { get; set; } = string.Empty;
}
