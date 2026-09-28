namespace BlazorFluent.Core.Domain.Delegates;

/// <summary>
/// Marker contract for any entity that is scoped to a specific project.
/// Used by the fail-closed ProjectSecurityInterceptor and project query filters.
/// </summary>
public interface IProjectScopedEntity
{
    Guid ProjectId { get; set; }
}
