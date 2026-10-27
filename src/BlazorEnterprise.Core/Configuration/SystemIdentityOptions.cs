namespace BlazorEnterprise.Core.Configuration;

/// <summary>
/// Strongly-typed configuration options for system daemon accounts and automated actors.
/// Section: "Identity"
/// Configured dynamically via KeyVault, Environment Variables, or User Secrets.
/// </summary>
public class SystemIdentityOptions
{
    public const string SectionName = "Identity";

    /// <summary>
    /// Local-part prefix for tenant daemon accounts (e.g. "daemon").
    /// </summary>
    public string SystemDaemonLocalPart { get; set; } = string.Empty;

    /// <summary>
    /// Domain suffix for tenant daemon accounts (e.g. "internal.local").
    /// </summary>
    public string SystemDaemonEmailDomain { get; set; } = string.Empty;

    /// <summary>
    /// Label recorded in audit trails and event tracking for system-originated operations (e.g. "System").
    /// </summary>
    public string SystemActorName { get; set; } = string.Empty;
}
