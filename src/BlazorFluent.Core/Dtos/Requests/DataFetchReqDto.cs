using BlazorFluent.Core.DataListTypes;
using System;
using System.Collections.Generic;
using System.Text;

namespace BlazorFluent.Core.Dtos.Requests
{
    abstract public class DataFetchReqDto {
        public string TenantId { get; set; }
        public string? UserId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? SearchTerm { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
    public class AuditTrailFilterReqDto: DataFetchReqDto
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

}
