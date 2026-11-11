using BlazorEnterprise.Shared.DataListTypes;

namespace BlazorEnterprise.Core.Domain.Base;

/// <summary>
/// Architectural base class for entities that can be scoped to a governing program.
/// Inherits mandatory TenantId isolation and introduces nullable ProgramId.
/// </summary>
public abstract class ProgramScopedEntity : TenantScopedEntity
{
    public Guid? ProgramId { get; set; }
}
