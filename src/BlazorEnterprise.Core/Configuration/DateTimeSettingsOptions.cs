namespace BlazorEnterprise.Core.Configuration;

/// <summary>
/// Strongly typed configuration options for system date-time handling and UTC enforcement.
/// Section: "DateTimeSettings"
/// </summary>
public class DateTimeSettingsOptions
{
    public const string SectionName = "DateTimeSettings";

    public bool UseUtc { get; set; } = true;
}
