namespace BlazorEnterprise.Core.Configuration;

/// <summary>
/// Configuration options for local developer sign-in capabilities.
/// Strictly forbidden in non-Development hosting environments.
/// </summary>
public sealed class DevLoginOptions
{
    public const string SectionName = "Auth:DevLogin";

    /// <summary>
    /// Whether developer passwordless email login is active. Must ONLY be true in local development.
    /// </summary>
    public bool Enabled { get; set; }
}
