using BlazorEnterprise.Shared.DataListTypes;

namespace BlazorEnterprise.Core.Domain.Base;

/// <summary>
/// Architectural base class for entities scoped to a project or multi-level work items/resources.
/// Inherits TenantId, ProgramId, and introduces nullable ProjectId.
/// Invariants are enforced via EntityValidationInterceptor based on the Level field.
/// </summary>
public abstract class ProjectScopedEntity : ProgramScopedEntity
{
    public Guid? ProjectId { get; set; }
}
