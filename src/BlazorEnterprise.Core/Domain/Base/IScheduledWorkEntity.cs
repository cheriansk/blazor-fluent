namespace BlazorEnterprise.Core.Domain.Base;

/// <summary>
/// ⚠️ ARCHITECTURAL CONTRACT: SCHEDULED WORK ITEM & LIFECYCLE PROGRESS
/// Extends <see cref="IEffectiveDatedEntity"/> to provide full Baseline vs. Actual schedule tracking.
/// Implemented by actionable work items (Programs, Projects, Tasks, SubTasks, RAID items, Workflows).
/// <para>
/// <b>Governance Rule:</b> Baseline commitments (<see cref="IEffectiveDatedEntity.StartDate"/>, <see cref="DueDate"/>, 
/// <see cref="IEffectiveDatedEntity.EndDate"/>, and <see cref="RevisedDueDate"/>) may only be modified by Project Leads,
/// Program Admins, or Tenant Admins.
/// </para>
/// <para>
/// <b>State Automation:</b> <see cref="ActualStartDate"/> is stamped automatically when work moves to InProgress.
/// <see cref="ActualEndDate"/> is stamped automatically when work reaches Completed or Closed.
/// </para>
/// </summary>
public interface IScheduledWorkEntity : IEffectiveDatedEntity
{
    /// <summary>
    /// Baseline delivery target deadline (UTC).
    /// Initial commitment established at creation; drives cadence notifications and variance tracking.
    /// </summary>
    DateTime DueDate { get; set; }

    /// <summary>
    /// Formally approved target deadline extension (UTC).
    /// Set when a Lead or Administrator approves a revised schedule without erasing the original baseline <see cref="DueDate"/>.
    /// </summary>
    DateTime? RevisedDueDate { get; set; }

    /// <summary>
    /// Actual timestamp (UTC) when work execution commenced.
    /// Automatically captured upon transitioning to InProgress, or manually updated with work logs.
    /// </summary>
    DateTime? ActualStartDate { get; set; }

    /// <summary>
    /// Actual timestamp (UTC) when work execution concluded.
    /// Automatically captured upon transitioning to Completed or Closed.
    /// </summary>
    DateTime? ActualEndDate { get; set; }

    /// <summary>
    /// Computed schedule delivery variance in calendar days.
    /// Positive values indicate delivery past the committed deadline (slippage / delay).
    /// Negative or zero values indicate on-time or early delivery.
    /// </summary>
    int ScheduleVarianceDays =>
        ((ActualEndDate ?? DateTime.UtcNow).Date - (RevisedDueDate ?? DueDate).Date).Days;
}
