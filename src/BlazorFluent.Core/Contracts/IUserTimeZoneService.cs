namespace BlazorFluent.Core.Contracts;

/// <summary>
/// Service contract for managing user timezone preferences and formatting dates/times across the application.
/// </summary>
public interface IUserTimeZoneService
{
    /// <summary>
    /// Current selected timezone ID (e.g. "UTC", "Eastern Standard Time", "America/New_York").
    /// Defaults to "UTC".
    /// </summary>
    string TimeZoneId { get; }

    /// <summary>
    /// The resolved TimeZoneInfo instance.
    /// </summary>
    TimeZoneInfo TimeZone { get; }

    /// <summary>
    /// All available system time zones for selection.
    /// </summary>
    IReadOnlyList<TimeZoneInfo> AvailableTimeZones { get; }

    /// <summary>
    /// Converts a UTC or unspecified DateTime to the user's configured timezone.
    /// </summary>
    DateTime? ToUserTime(DateTime? utcDateTime);

    /// <summary>
    /// Converts a UTC or unspecified DateTime to the user's configured timezone.
    /// </summary>
    DateTime ToUserTime(DateTime utcDateTime);

    /// <summary>
    /// Converts a user local DateTime to UTC DateTime.
    /// </summary>
    DateTime ToUtc(DateTime userDateTime);

    /// <summary>
    /// Formats a DateTime into a string formatted according to the user's timezone.
    /// </summary>
    string FormatDateTime(DateTime? dateTime, string format = "yyyy-MM-dd HH:mm:ss");

    /// <summary>
    /// Formats a Date into a string formatted according to the user's timezone.
    /// </summary>
    string FormatDate(DateTime? dateTime, string format = "yyyy-MM-dd");

    /// <summary>
    /// Updates the user's timezone preference, persisting to session and database.
    /// </summary>
    Task SetTimeZoneAsync(string timeZoneId);

    /// <summary>
    /// Instantly applies a known user timezone without needing redundant storage lookups.
    /// </summary>
    void ApplyUserTimeZone(string? timeZoneId);

    /// <summary>
    /// Initializes or syncs the timezone for the active user session.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Event triggered when the user's timezone selection changes.
    /// </summary>
    event Action? OnChange;
}
