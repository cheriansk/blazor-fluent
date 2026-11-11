using BlazorEnterprise.Core.Domain.Base;
using BlazorEnterprise.Core.Domain.Tasks;
using BlazorEnterprise.Core.Domain.Versioning;
using BlazorEnterprise.Shared.DataListTypes;

namespace BlazorEnterprise.Core.Domain.Sprints;

/// <summary>
/// Versioned assignment of a task item to a sprint iteration, tracking movement and effort across sprint versions.
/// Implements IVersionedEntity.
/// </summary>
public class SprintItemVersionEntity : ProjectScopedEntity, IVersionedEntity, ISoftDeletableEntity
{
    // Inherits ProjectId from ProjectScopedEntity

    public required Guid ItemId { get; set; }

    public Guid? SprintId { get; set; }

    public double EstimatedHours { get; set; }

    public double ActualHours { get; set; }

    public TaskStatusEnum Status { get; set; }

    public DateTime EffectiveFromDate { get; set; }

    public DateTime? EffectiveToDate { get; set; }

    // --- ISprintVersionEntity / IVersionedEntity ---
    public Guid RevisionHeaderId { get; set; }
    public Guid RelId { get; set; }
    public int VerNum { get; set; }
    public Guid ParentRowId { get; set; }

    public void SetVersionNumber(int newVersion)
    {
        if (newVersion <= VerNum)
        {
            throw new InvalidOperationException($"New version ({newVersion}) must be greater than current version ({VerNum}).");
        }
        VerNum = newVersion;
    }

    // --- ISoftDeletableEntity ---
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }

    // --- Navigation Properties ---
    public SprintEntity? Sprint { get; set; }
    public TaskEntity? Item { get; set; }
    public RevisionHeaderEntity? RevisionHeader { get; set; }
}
