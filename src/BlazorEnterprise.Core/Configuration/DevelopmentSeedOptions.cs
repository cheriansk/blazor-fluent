namespace BlazorEnterprise.Core.Configuration;

/// <summary>
/// Configuration-driven seed options for initializing a local development database.
/// Eradicates hardcoded tenant slugs, domains, and administrative identities from compiled code.
/// </summary>
public sealed class DevelopmentSeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>
    /// Whether automated database seeding is enabled in local development.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Initial root tenant slug (e.g. from configuration, NOT hardcoded).
    /// </summary>
    public string TenantSlug { get; set; } = string.Empty;

    /// <summary>
    /// Initial root tenant display name.
    /// </summary>
    public string TenantName { get; set; } = string.Empty;

    /// <summary>
    /// Semicolon- or comma-separated allowed internal email domains for the tenant.
    /// </summary>
    public string InternalEmailDomains { get; set; } = string.Empty;

    /// <summary>
    /// Semicolon- or comma-separated allowed external email domains for the tenant.
    /// </summary>
    public string ExternalEmailDomains { get; set; } = string.Empty;

    /// <summary>
    /// Initial root administrator email address.
    /// </summary>
    public string AdminEmail { get; set; } = string.Empty;

    /// <summary>
    /// Initial root administrator full display name.
    /// </summary>
    public string AdminFullName { get; set; } = string.Empty;
}
