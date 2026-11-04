namespace BlazorEnterprise.Core.Configuration;

/// <summary>
/// Strongly typed configuration options for database command logging and parameter inspection.
/// Section: "Logging"
/// </summary>
public class DatabaseLoggingOptions
{
    public const string SectionName = "Logging";

    public bool SplitLogFiles { get; set; } = true;
    public bool EnableDbLogging { get; set; } = true;
    public bool LogDbParameters { get; set; } = true;
}
