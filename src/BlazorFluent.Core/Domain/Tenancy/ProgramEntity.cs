using System.ComponentModel.DataAnnotations.Schema;
using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Core.Domain.Base;

namespace BlazorFluent.Core.Domain.Tenancy;

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
}
