using System.ComponentModel.DataAnnotations.Schema;
using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Core.Domain.Base;

namespace BlazorFluent.Core.Domain.Tenancy;

/// <summary>
/// A project is always scoped to exactly one tenant via sequential UUIDv7 <see cref="TenantAuditableEntity.TenantId"/>.
/// Implements <see cref="IScheduledWorkEntity"/> for baseline vs. actual schedule tracking.
/// Implements <see cref="ISoftDeletableEntity"/> — projects are soft-deleted, not physically removed.
/// </summary>
public class ProjectEntity : TenantAuditableEntity, IScheduledWorkEntity, ISoftDeletableEntity
{
    public required string Name { get; set; }

    public required string ShortCode { get; set; }

    public string? Description { get; set; }

    public string? Location { get; set; }

    public string? ScopeSummary { get; set; }

    public required ProjectStatusEnum Status { get; set; }

    public bool IsActive { get; set; }

    /// <summary>
    /// Dedicated Microsoft Teams incoming webhook URL for project-specific notifications and daily task digests.
    /// If null or empty, falls back to the system-wide default Teams webhook.
    /// </summary>
    public string? TeamsWebhookUrl { get; set; }

    // --- IScheduledWorkEntity & IEffectiveDatedEntity ---

    /// <summary>Baseline planned project start date (UTC).</summary>
    public required DateTime StartDate { get; set; }

    /// <summary>Baseline planned project delivery deadline (UTC).</summary>
    public required DateTime DueDate { get; set; }

    /// <summary>Formally approved target deadline extension (UTC).</summary>
    public DateTime? RevisedDueDate { get; set; }

    /// <summary>Baseline planned project target completion/close date (UTC).</summary>
    public DateTime? EndDate { get; set; }

    /// <summary>Actual timestamp (UTC) when project execution commenced.</summary>
    public DateTime? ActualStartDate { get; set; }

    /// <summary>Actual timestamp (UTC) when project was completed or closed.</summary>
    public DateTime? ActualEndDate { get; set; }

    // --- Backward-Compatible Navigation Forwarders ---
    [NotMapped]
    public DateTime? TentativeStartDate { get => StartDate; set => StartDate = value ?? default; }

    [NotMapped]
    public DateTime? TentativeEndDate { get => EndDate; set => EndDate = value; }

    // --- ISoftDeletableEntity ---
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }

    /// <summary>Navigation to parent tenant (relational FK is <see cref="TenantAuditableEntity.TenantId"/>).</summary>
    public TenantEntity? TenantEntity { get; set; }

    /// <summary>FK to the governing <see cref="ProgramEntity"/>.</summary>
    public Guid? ProgramId { get; set; }

    /// <summary>Navigation to parent program.</summary>
    public ProgramEntity? Program { get; set; }

    /// <summary>Navigation to project user role assignments.</summary>
    public ICollection<ProjectUserRoleEntity> UserRoles { get; set; } = new List<ProjectUserRoleEntity>();
}
