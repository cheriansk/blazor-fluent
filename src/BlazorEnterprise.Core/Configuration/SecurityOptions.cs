namespace BlazorEnterprise.Core.Configuration;

/// <summary>
/// Strongly typed configuration options for system security, encryption keys, and root administrators.
/// Section: "Security"
/// </summary>
public class SecurityOptions
{
    public const string SectionName = "Security";

    public string IntegritySecret { get; set; } = string.Empty;
    public string MasterEncryptionKey { get; set; } = string.Empty;
    public string RootAdmins { get; set; } = string.Empty;
    public string SystemBootstrapEmail { get; set; } = string.Empty;
}
