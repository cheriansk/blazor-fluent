using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Delegates;

namespace BlazorFluent.Core.Domain.Jobs;

/// <summary>
/// Persistent record of a background batch job execution.
/// Stored in PostgreSQL with execution duration, attempt count, and error diagnostics.
/// Implements <see cref="IGlobalEntity"/> (host-wide visibility) and <see cref="IAuditExemptEntity"/> (prevents recursive auditing).
/// </summary>
public class JobExecutionEntity : AuditableEntity, IGlobalEntity, IAuditExemptEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Correlation identifier linking all chained batch steps in the same pipeline.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>Identifier of the predecessor job execution that triggered this step (if chained).</summary>
    public Guid? ParentExecutionId { get; set; }

    /// <summary>Name or type of the background batch job.</summary>
    public string JobName { get; set; } = string.Empty;

    /// <summary>Originating tenant identifier, or null/host for system jobs.</summary>
    public string? TenantId { get; set; }

    /// <summary>How the job was initiated: 'Cron', 'Manual', 'Event', 'Manual Retry'.</summary>
    public string TriggerSource { get; set; } = "Cron";


    /// <summary>Current lifecycle status.</summary>
    public JobStatus Status { get; set; } = JobStatus.Queued;

    /// <summary>Current attempt number (1 to MaxRetries).</summary>
    public int AttemptCount { get; set; } = 1;

    /// <summary>Maximum number of attempts before permanent failure.</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>Optional serialized payload or parameters passed to the job.</summary>
    public string? PayloadJson { get; set; }

    /// <summary>Exception message or diagnostic details if an attempt failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Timestamp when the job execution began.</summary>
    public DateTime StartedAt { get; set; }

    /// <summary>Timestamp when the job execution finished (succeeded or fatally failed).</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>Total wall-clock execution duration in milliseconds.</summary>
    public double? DurationMs { get; set; }
}
