using BlazorEnterprise.Core.Domain.Base;
using BlazorEnterprise.Core.Domain.Versioning;

namespace BlazorEnterprise.Core.Domain.Resources;

/// <summary>
/// Hourly billing/cost rate assigned to a project resource within a versioned plan.
/// </summary>
public class ResourceRateEntity : TenantAuditableEntity, IVersionedEntity, IProjectScopedEntity, ISoftDeletableEntity
{
    public required Guid ProjectId { get; set; }

    public required Guid ProjectResourceId { get; set; }

    public decimal HourlyRate { get; set; }

    public string CurrencyCode { get; set; } = "USD";

    public DateTime? EffectiveFromDate { get; set; }

    public DateTime? EffectiveToDate { get; set; }

    // --- IVersionedEntity ---
    public Guid RevisionHeaderId { get; set; }
    public Guid RelId { get; set; }
    public int VerNum { get; set; } = 1;
    public Guid ParentRowId { get; set; } = Guid.Empty;

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
