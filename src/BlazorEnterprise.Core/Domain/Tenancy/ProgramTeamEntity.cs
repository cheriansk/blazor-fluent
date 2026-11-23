// [NEED-HUMAN-REVIEW]
using BlazorEnterprise.Core.Domain.Base;

namespace BlazorEnterprise.Core.Domain.Tenancy;

/// <summary>
/// Program-scoped team accessible by any project within the same program.
/// </summary>
public class ProgramTeamEntity : TenantScopedEntity, ISoftDeletableEntity, IUnversionedEntity
{
    public Guid ProgramId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> OwnerNames { get; set; } = [];
    public bool IsDisabled { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}
