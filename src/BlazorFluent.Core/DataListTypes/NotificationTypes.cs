using BlazorFluent.Core.Attributes;
using System.ComponentModel.DataAnnotations;

namespace BlazorFluent.Core.DataListTypes;

/// <summary>
/// Broad categorization of the notification scope.
/// </summary>
public enum NotificationCategory
{
    /// <summary>
    /// Generic notification visible to all authorized project/tenant members.
    /// </summary>
    [Display(Name = "Generic / Project", Description = "Visible to all project members")]
    [DataListCategory(NotificationDefinitions.Categories.Audience)]
    Generic = 1,

    /// <summary>
    /// Personal notification strictly isolated to a single recipient user.
    /// </summary>
    [Display(Name = "Personal", Description = "Strictly isolated to recipient user")]
    [DataListCategory(NotificationDefinitions.Categories.Audience)]
    Personal = 2,
}

/// <summary>
/// Severity level indicating visual styling and alert priority.
/// </summary>
public enum NotificationSeverity
{
    [Display(Name = "Information", Description = "Informational message")]
    [DataListCategory(NotificationDefinitions.Categories.Severity)]
    Info = 1,

    [Display(Name = "Success", Description = "Operation succeeded")]
    [DataListCategory(NotificationDefinitions.Categories.Severity)]
    Success = 2,

    [Display(Name = "Warning", Description = "Warning condition")]
    [DataListCategory(NotificationDefinitions.Categories.Severity)]
    Warning = 3,

    [Display(Name = "Error", Description = "Critical failure or error")]
    [DataListCategory(NotificationDefinitions.Categories.Severity)]
    Error = 4
}

/// <summary>
/// Bitmask channels for notification dispatch.
/// </summary>
[Flags]
public enum NotificationChannels
{
    [Display(Name = "None")]
    None = 0,

    [Display(Name = "In-App Bell", Description = "Real-time in-app notification bell")]
    [DataListCategory(NotificationDefinitions.Categories.Channel)]
    InApp = 1,

    [Display(Name = "Microsoft Teams", Description = "Outbound Microsoft Teams webhook MessageCard")]
    [DataListCategory(NotificationDefinitions.Categories.Channel)]
    Teams = 2,

    [Display(Name = "Email", Description = "SMTP outbound HTML email")]
    [DataListCategory(NotificationDefinitions.Categories.Channel)]
    Email = 4,

    [Display(Name = "All Channels", Description = "Broadcast to all delivery channels")]
    All = InApp | Teams | Email
}

public static class NotificationDefinitions
{
    public static class Categories
    {
        [Display(Name = "Audience Scope", Description = "Categorization of notification recipients")]
        public const string Audience = "Audience";

        [Display(Name = "Alert Severity", Description = "Visual priority and styling")]
        public const string Severity = "Severity";

        [Display(Name = "Delivery Channel", Description = "Transmission mechanism")]
        public const string Channel = "Channel";
    }
}
