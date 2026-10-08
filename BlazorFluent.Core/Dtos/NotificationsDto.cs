using BlazorFluent.Core.DataListTypes;

namespace BlazorFluent.Core.Dtos;

/// <summary>
/// Command to publish and dispatch a notification.
/// </summary>
public record SendNotificationReqDto
{
    public Guid ProjectId { get; init; } = Guid.Empty;
    public string? UserId { get; init; }
    public NotificationCategory Category { get; init; } = NotificationCategory.Generic;
    public NotificationSeverity Severity { get; init; } = NotificationSeverity.Info;
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? LinkUrl { get; init; }
    public NotificationChannels Channels { get; init; } = NotificationChannels.InApp;
    public string? MetadataJson { get; init; }
}

/// <summary>
/// Query filter for retrieving notifications.
/// </summary>
public record NotificationFilterReqDto
{
    public NotificationCategory Category { get; init; } = NotificationCategory.Generic;
    public bool OnlyUnread { get; init; }
    public Guid? ProjectId { get; init; }
    public int Take { get; init; } = 30;
}

/// <summary>
/// Aggregated unread notification counters.
/// </summary>
public record UnreadNotificationCountsRespDto
{
    public int GenericUnread { get; init; }
    public int PersonalUnread { get; init; }
    public int TotalUnread => GenericUnread + PersonalUnread;
}
