using BlazorFluent.Core.DataListTypes;

namespace BlazorFluent.Core.Common;

public class AuditTrailFilter
{
    public string? TenantId { get; set; }
    public AuditEventType? EventType { get; set; }
    public AuditSeverity? Severity { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? SearchTerm { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 15;
}
