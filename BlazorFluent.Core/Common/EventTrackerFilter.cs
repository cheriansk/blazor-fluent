using BlazorFluent.Core.DataListTypes;

namespace BlazorFluent.Core.Common;

public class EventTrackerFilter
{
    public string? TenantId { get; set; }
    public EventTrackerStatus? Status { get; set; }
    public string? EventName { get; set; }
    public string? SourceClass { get; set; }
    public string? UserId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? SearchTerm { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
