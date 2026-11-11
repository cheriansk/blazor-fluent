using BlazorEnterprise.Core.Domain.Base;
using BlazorEnterprise.Core.Domain.Versioning;

namespace BlazorEnterprise.Core.Domain.Resources;

/// <summary>
/// Hourly billing/cost rate assigned to a project resource within a versioned plan.
/// </summary>
public class ResourceRateEntity : ProjectScopedEntity, IVersionedEntity, ISoftDeletableEntity
{
    // Inherits ProjectId from ProjectScopedEntity

    public required Guid ProjectResourceId { get; set; }

    public decimal HourlyRate { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public DateTime? EffectiveFromDate { get; set; }

    public DateTime? EffectiveToDate { get; set; }

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
