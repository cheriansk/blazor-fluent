using BlazorEnterprise.Shared.Attributes;
using BlazorEnterprise.Shared.DataListTypes;

namespace BlazorEnterprise.Core.Dtos;

/// <summary>
/// Command to publish and dispatch a notification.
/// </summary>
public record SendNotificationReqDto
{
    public Guid ProjectId { get; init; }
    public string? UserId { get; init; }
    [AllowHardcodedDefault("Standard baseline category for system notifications")]
    public NotificationCategoryEnum Category { get; init; } = NotificationCategoryEnum.Generic;
    [AllowHardcodedDefault("Standard baseline severity for informational notifications")]
    public NotificationSeverityEnum Severity { get; init; } = NotificationSeverityEnum.Info;
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? LinkUrl { get; init; }
    [AllowHardcodedDefault("Default delivery channel is InApp notification")]
    public NotificationChannelsEnum Channels { get; init; } = NotificationChannelsEnum.InApp;
    public string? MetadataJson { get; init; }
}

/// <summary>
/// Query filter for retrieving notifications.
/// </summary>
public record NotificationFilterReqDto
{
    public NotificationCategoryEnum? Category { get; init; }
    public bool OnlyUnread { get; init; }
    public bool? IsRead { get; init; }
    public NotificationSeverityEnum? Severity { get; init; }
    public Guid? TenantId { get; init; }
    public Guid? ProjectId { get; init; }
    public DateTime? FromDateUtc { get; init; }
    public DateTime? ToDateUtc { get; init; }
    public string? SearchTerm { get; init; }
    public int Skip { get; init; }
    [AllowHardcodedDefault("Default query page size is 30")]
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
