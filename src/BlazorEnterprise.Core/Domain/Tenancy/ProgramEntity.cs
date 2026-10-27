using System.ComponentModel.DataAnnotations.Schema;
using BlazorEnterprise.Shared.DataListTypes;
using BlazorEnterprise.Shared.Extensions;
using BlazorEnterprise.Core.Domain.Base;

namespace BlazorEnterprise.Core.Domain.Tenancy;

/// <summary>
/// Represents an enterprise multi-year program containing one or more projects.
/// Scoped to a tenant via sequential UUIDv7 <see cref="TenantAuditableEntity.TenantId"/>.
/// Implements <see cref="IScheduledWorkEntity"/> for baseline vs. actual schedule tracking.
/// Implements <see cref="ISoftDeletableEntity"/> — programs are soft-deleted to maintain audit trails.
/// </summary>
public class ProgramEntity : TenantAuditableEntity, IScheduledWorkEntity, ISoftDeletableEntity
{
    public required string Name { get; set; }

    public required string Code { get; set; }

    public string? Description { get; set; }

    public required ProgramStatusEnum Status { get; set; }

    public bool IsActive { get; set; }

    // --- IScheduledWorkEntity & IEffectiveDatedEntity ---

    /// <summary>Baseline planned start date (UTC).</summary>
    public required DateTime StartDate { get; set; }

    /// <summary>Baseline planned delivery deadline (UTC).</summary>
    public required DateTime DueDate { get; set; }

    /// <summary>Formally approved target deadline extension (UTC).</summary>
    public DateTime? RevisedDueDate { get; set; }

    /// <summary>Baseline planned program target close date (UTC).</summary>
    public DateTime? EndDate { get; set; }

    /// <summary>Actual timestamp (UTC) when program execution commenced.</summary>
    public DateTime? ActualStartDate { get; set; }

    /// <summary>Actual timestamp (UTC) when program was finalized or closed.</summary>
    public DateTime? ActualEndDate { get; set; }

    /// <summary>Actual timestamp (UTC) when program status transitioned to Closed or Completed.</summary>
    public DateTime? ClosedAtUtc { get; set; }

    // --- Backward-Compatible Navigation Forwarders ---
    [NotMapped]
    public DateTime? StartDateUtc { get => StartDate; set => StartDate = value ?? default; }

    [NotMapped]
    public DateTime? TargetEndDateUtc { get => EndDate; set => EndDate = value; }

    // --- ISoftDeletableEntity ---
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }

    /// <summary>Navigation to parent tenant (relational FK is <see cref="TenantAuditableEntity.TenantId"/>).</summary>
    public TenantEntity? TenantEntity { get; set; }

    /// <summary>Navigation to member projects under this program.</summary>
    public ICollection<ProjectEntity> Projects { get; set; } = new List<ProjectEntity>();

    /// <summary>Navigation to assigned program-level governance roles.</summary>
    public ICollection<ProgramUserRoleEntity> UserRoles { get; set; } = new List<ProgramUserRoleEntity>();

    /// <summary>
    /// Executes a validated state transition for this program, atomically managing lifecycle timestamps.
    /// </summary>
    public virtual void TransitionTo(ProgramStatusEnum nextStatus, DateTime utcNow, string reason = "")
    {
        if (Status == nextStatus) return;
        if (!Status.CanTransitionTo(nextStatus))
        {
            throw new InvalidOperationException($"Cannot transition program '{Name}' from {Status} to {nextStatus}.");
        }

        Status = nextStatus;

        if (nextStatus == ProgramStatusEnum.Active && ActualStartDate == null)
        {
            ActualStartDate = utcNow;
        }
        else if (nextStatus == ProgramStatusEnum.Completed)
        {
            ActualEndDate = utcNow;
            ClosedAtUtc = utcNow;
        }
        else if (nextStatus is ProgramStatusEnum.Planning or ProgramStatusEnum.Active && ActualEndDate != null)
        {
            ActualEndDate = null;
            ClosedAtUtc = null;
        }
    }

    /// <summary>Activates this program.</summary>
    public virtual void Activate(DateTime utcNow) => TransitionTo(ProgramStatusEnum.Active, utcNow);

    /// <summary>Marks this program as at risk with a stated reason.</summary>
    public virtual void MarkAtRisk(DateTime utcNow, string reason = "") => TransitionTo(ProgramStatusEnum.AtRisk, utcNow, reason: reason);

    /// <summary>Suspends this program with a stated reason.</summary>
    public virtual void PutOnHold(DateTime utcNow, string reason = "") => TransitionTo(ProgramStatusEnum.OnHold, utcNow, reason: reason);

    /// <summary>Completes this program.</summary>
    public virtual void Complete(DateTime utcNow) => TransitionTo(ProgramStatusEnum.Completed, utcNow);
}
