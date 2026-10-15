using BlazorFluent.Shared.DataListTypes;

namespace BlazorFluent.Shared.Contracts.Common;

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
    public AuditEventType? EventType { get; set; }
    public AuditSeverity? Severity { get; set; }
}

public class EventTrackerFilterReqDto : DataFetchReqDto
{
    public EventTrackerStatus? Status { get; set; }
    public string? EventName { get; set; }
    public string? SourceClass { get; set; }
}
