using BlazorEnterprise.Core.Domain.Base;
using BlazorEnterprise.Core.Domain.Versioning;

namespace BlazorEnterprise.Core.Domain.Resources;

/// <summary>
/// Daily resource allocation percentage belonging to a versioned allocation plan.
/// </summary>
public class ResourceAllocationEntity : ProjectScopedEntity, IVersionedEntity, ISoftDeletableEntity
{
    // Inherits ProjectId from ProjectScopedEntity

    public required Guid ProjectResourceId { get; set; }

    public required DateTime AllocationDate { get; set; }

    /// <summary>
    /// Allocation percentage for this day (0 - 100).
    /// </summary>
    public int AllocationPercentage { get; set; }

    // --- IVersionedEntity ---
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
    public ProjectResourceEntity? ProjectResource { get; set; }
    public RevisionHeaderEntity? RevisionHeader { get; set; }
}
