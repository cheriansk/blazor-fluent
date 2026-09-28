using BlazorFluent.Core.Domain.Delegates;

namespace BlazorFluent.Core.Common;

/// <summary>
/// Query extensions for enforcing project-level authorization filters in EF Core.
/// </summary>
public static class ProjectAuthorizationExtensions
{
    /// <summary>
    /// Restricts a queryable sequence of project-scoped entities to only those projects
    /// the caller is authorized to access.
    /// </summary>
    public static IQueryable<T> WhereAuthorizedProject<T>(
        this IQueryable<T> query,
        IEnumerable<Guid> authorizedProjectIds)
        where T : class, IProjectScopedEntity
    {
        var idList = authorizedProjectIds as IReadOnlyList<Guid> ?? authorizedProjectIds.ToList();
        return query.Where(entity => idList.Contains(entity.ProjectId));
    }
}
