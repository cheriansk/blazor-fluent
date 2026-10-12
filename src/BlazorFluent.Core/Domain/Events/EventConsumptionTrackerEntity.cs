using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Base;

namespace BlazorFluent.Core.Domain.Events;

/// <summary>
/// Persistent ledger record capturing an individual consumer execution receipt:
/// which handler class and method consumed it, when it ran, duration in ms, and diagnostics.
/// Implements <see cref="ITenantEntity"/> for tenancy scoping,
/// <see cref="IAuditExemptEntity"/> to prevent recursive auditing,
/// and <see cref="IValidationExemptEntity"/> since it represents an immutable execution record.
/// </summary>
public class EventConsumptionTrackerEntity : AuditableEntity, ITenantEntity, IAuditExemptEntity, IValidationExemptEntity
{
    /// <summary>Tenant scope for row-level isolation.</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Foreign key linking to the parent publication record.</summary>
    public Guid EventPublishTrackerId { get; set; }

    /// <summary>Navigation property to the parent event publication tracker.</summary>
    public EventPublishTrackerEntity? PublishTracker { get; set; }

    /// <summary>Correlation identifier matching the published event.</summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>Name of the consumer or handler class that processed the event.</summary>
    public string ConsumerClass { get; set; } = string.Empty;

    /// <summary>Name of the handler method invoked (e.g. 'HandleAsync').</summary>
    public string ConsumerMethod { get; set; } = string.Empty;

    /// <summary>Timestamp (UTC) when consumer execution began.</summary>
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Timestamp (UTC) when consumer execution finished.</summary>
    public DateTime? CompletedAtUtc { get; set; }

    /// <summary>Wall-clock execution duration in milliseconds.</summary>
    public double DurationMs { get; set; }

    /// <summary>Execution status of this specific consumer.</summary>
    public EventConsumptionStatus Status { get; set; } = EventConsumptionStatus.Running;

    /// <summary>Attempt number for this consumer execution.</summary>
    public int AttemptCount { get; set; } = 1;

    /// <summary>Error message or exception summary if the consumer execution failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Detailed stack trace or diagnostic details if an exception was caught.</summary>
    public string? ExceptionDetails { get; set; }
}
