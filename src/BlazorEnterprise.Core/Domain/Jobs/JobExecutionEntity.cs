using BlazorEnterprise.Shared.DataListTypes;
using BlazorEnterprise.Core.Domain.Base;

namespace BlazorEnterprise.Core.Domain.Jobs;

/// <summary>
/// Persistent record of a background batch job execution.
/// Stored in PostgreSQL with execution duration, attempt count, and error diagnostics.
/// <para>
/// <b>Architectural Tenancy Rationale:</b> Implements <see cref="IGlobalEntity"/> (opting out of EF Core's automatic
/// <c>QueryFilters.Tenant</c>) to allow platform-wide telemetry, cross-tenant operational oversight, and execution by
/// unauthenticated system daemons (e.g. maintenance jobs with <c>TenantId = "HOST"</c> or <c>"default"</c>).
/// Multi-tenant isolation for tenant users is strictly enforced at the application boundary via <c>IJobManagerService</c>.
/// </para>
/// <para>
/// Implements <see cref="IAuditExemptEntity"/> to prevent recursive auditing loops, and <see cref="IImmutableEntity"/> to enforce ledger integrity.
/// </para>
/// </summary>
public class JobExecutionEntity : AuditableEntity, IGlobalEntity, IAuditExemptEntity, IImmutableEntity
{
    /// <summary>Mandatory correlation identifier linking all chained batch steps in the same pipeline.</summary>
    public required string CorrelationId { get; set; }

    /// <summary>Identifier of the predecessor job execution that triggered this step (if chained).</summary>
    public Guid? ParentExecutionId { get; set; }

    /// <summary>Name or type of the background batch job.</summary>
    public required string JobName { get; set; }

    /// <summary>Originating tenant identifier, or 'HOST' for platform-wide system jobs.</summary>
    public required Guid TenantId { get; set; }

    /// <summary>Strongly-typed trigger mechanism that initiated the execution.</summary>
    public required JobTriggerSourceEnum TriggerSource { get; set; }


    /// <summary>Current lifecycle status.</summary>
    public required JobStatusEnum Status { get; set; }

    /// <summary>Current attempt number (1 to MaxRetries).</summary>
    public required int AttemptCount { get; set; }

    /// <summary>Maximum number of attempts before permanent failure.</summary>
    public required int MaxRetries { get; set; }

    /// <summary>Optional serialized payload or parameters passed to the job.</summary>
    public string? PayloadJson { get; set; }

    /// <summary>Exception message or diagnostic details if an attempt failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Timestamp when the job execution began.</summary>
    public required DateTime StartedAt { get; set; }

    /// <summary>Timestamp when the job execution finished (succeeded or fatally failed).</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>Total wall-clock execution duration in milliseconds.</summary>
    public double? DurationMs { get; set; }
}
