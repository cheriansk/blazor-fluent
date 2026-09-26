namespace BlazorFluent.Core.Common;

public class DateTimeSettings
{
    public const string SectionName = "DateTimeSettings";

    /// <summary>
    /// If true, UTC is used for database auditing timestamps and logging.
    /// If false, the server's local system timezone is used.
    /// </summary>
    public bool UseUtc { get; set; } = true;
}
