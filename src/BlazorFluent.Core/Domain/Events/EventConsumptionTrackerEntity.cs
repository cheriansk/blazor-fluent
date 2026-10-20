using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Core.Domain.Base;

namespace BlazorFluent.Core.Domain.Events;

/// <summary>
/// Persistent ledger record capturing an individual consumer execution receipt:
/// which handler class and method consumed it, when it ran, duration in ms, and diagnostics.
/// Implements <see cref="ITenantEntity"/> for tenancy scoping,
/// <see cref="IAuditExemptEntity"/> to prevent recursive auditing,
/// </summary>
public class EventConsumptionTrackerEntity : AuditableEntity, ITenantEntity, IAuditExemptEntity, IImmutableEntity
{
    /// <summary>Tenant scope for row-level isolation.</summary>
    public required string TenantId { get; set; }

    /// <summary>Foreign key linking to the parent publication record.</summary>
    public required Guid EventPublishTrackerId { get; set; }

    /// <summary>Navigation property to the parent event publication tracker.</summary>
    public EventPublishTrackerEntity? PublishTracker { get; set; }

    /// <summary>Correlation identifier matching the published event.</summary>
    public required string CorrelationId { get; set; }

    /// <summary>Name of the consumer or handler class that processed the event.</summary>
    public required string ConsumerClass { get; set; }

    /// <summary>Name of the handler method invoked (e.g. 'HandleAsync').</summary>
    public required string ConsumerMethod { get; set; }

    /// <summary>Timestamp (UTC) when consumer execution began.</summary>
    public required DateTime StartedAtUtc { get; set; }

    /// <summary>Timestamp (UTC) when consumer execution finished.</summary>
    public DateTime? CompletedAtUtc { get; set; }

    /// <summary>Wall-clock execution duration in milliseconds.</summary>
    public double DurationMs { get; set; }

    /// <summary>Execution status of this specific consumer.</summary>
    public required EventConsumptionStatusEnum Status { get; set; }

    /// <summary>Attempt number for this consumer execution.</summary>
    public required int AttemptCount { get; set; }

    /// <summary>Error message or exception summary if the consumer execution failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Detailed stack trace or diagnostic details if an exception was caught.</summary>
    public string? ExceptionDetails { get; set; }
}
