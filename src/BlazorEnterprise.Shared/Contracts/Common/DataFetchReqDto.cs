using BlazorEnterprise.Shared.DataListTypes;

namespace BlazorEnterprise.Shared.Contracts.Common;

/// <summary>
/// Universal paging, sorting, and text filtering query request.
/// </summary>
public abstract class DataFetchReqDto
{
    public string TenantId { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? SearchTerm { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class AuditTrailFilterReqDto : DataFetchReqDto
{
    public List<string>? AllowedTenantIds { get; set; }
    public AuditEventTypeEnum? EventType { get; set; }
    public AuditSeverityEnum? Severity { get; set; }
}

public class EventTrackerFilterReqDto : DataFetchReqDto
{
    public EventTrackerStatusEnum? Status { get; set; }
    public string? EventName { get; set; }
    public string? SourceClass { get; set; }
}
