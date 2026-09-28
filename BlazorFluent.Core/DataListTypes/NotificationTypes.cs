namespace BlazorFluent.Core.DataListTypes;

/// <summary>
/// Broad categorization of the notification scope.
/// </summary>
public enum NotificationCategory
{
    /// <summary>
    /// Generic notification visible to all authorized project/tenant members.
    /// </summary>
    Generic = 1,

    /// <summary>
    /// Personal notification strictly isolated to a single recipient user.
    /// </summary>
    Personal = 2
}

/// <summary>
/// Severity level indicating visual styling and alert priority.
/// </summary>
public enum NotificationSeverity
{
    Info = 1,
    Success = 2,
    Warning = 3,
    Error = 4
}

/// <summary>
/// Bitmask channels for notification dispatch.
/// </summary>
[Flags]
public enum NotificationChannels
{
    None = 0,
    InApp = 1,
    Teams = 2,
    Email = 4,
    All = InApp | Teams | Email
}
