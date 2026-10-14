namespace BlazorFluent.Core.Configuration;

/// <summary>
/// Configuration options for local developer sign-in capabilities.
/// Strictly forbidden in non-Development hosting environments.
/// </summary>
public sealed class DevLoginOptions
{
    public const string SectionName = "Auth:DevLogin";

    /// <summary>
    /// Whether developer login sandbox is active. Must ONLY be true in local development.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Configuration-driven list of test user personas available in local development.
    /// Eliminates hardcoded test user emails from UI markup and compiled code.
    /// </summary>
    public List<DevLoginUserEntry> Users { get; set; } = [];
}

/// <summary>
/// Represents a test persona configured for local development sign-in.
/// </summary>
public sealed class DevLoginUserEntry
{
    public string Email { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}
