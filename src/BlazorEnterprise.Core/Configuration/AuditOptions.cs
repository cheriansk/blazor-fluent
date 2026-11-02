namespace BlazorEnterprise.Core.Configuration;

/// <summary>
/// Strongly typed configuration options for audit log retention and lifecycle management.
/// Section: "Audit"
/// </summary>
public class AuditOptions
{
    public const string SectionName = "Audit";

    public int RetentionDays { get; set; } = 365;
}
