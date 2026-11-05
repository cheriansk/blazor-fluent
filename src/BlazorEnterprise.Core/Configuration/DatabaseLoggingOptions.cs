namespace BlazorEnterprise.Core.Configuration;

/// <summary>
/// Configures EF Core SQL command logging behavior.
/// </summary>
public enum DatabaseLoggingMode
{
    /// <summary>Logs all SQL queries and command failures.</summary>
    All = 1,

    /// <summary>Only logs SQL commands when an exception occurs.</summary>
    ErrorsOnly = 2,

    /// <summary>Disables all command logging from the interceptor.</summary>
    None = 3
}

/// <summary>
/// Strongly typed configuration options for database command logging and parameter inspection.
/// Section: "Logging:Database"
/// </summary>
public class DatabaseLoggingOptions
{
    public const string SectionName = "Logging:Database";

    /// <summary>
    /// Governs whether all SQL queries, failed SQL queries only, or no queries are logged.
    /// Defaults to ErrorsOnly.
    /// </summary>
    public DatabaseLoggingMode Mode { get; set; } = DatabaseLoggingMode.ErrorsOnly;

    /// <summary>
    /// Whether to log query parameters. Sensitive parameters (passwords, secrets) are redacted.
    /// </summary>
    public bool LogDbParameters { get; set; } = true;

    /// <summary>
    /// Whether to prepend /* CorrId: <id> */ to the SQL command text so PostgreSQL logs and pg_stat_activity match the app log CorrelationId.
    /// Defaults to true.
    /// </summary>
    public bool TagSqlWithCorrelationId { get; set; } = true;
}
