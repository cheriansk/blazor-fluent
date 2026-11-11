using BlazorEnterprise.Core.Domain.Base;

namespace BlazorEnterprise.Core.Extensions;

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
        where T : ProjectScopedEntity
    {
        var idList = authorizedProjectIds as IReadOnlyList<Guid> ?? authorizedProjectIds.ToList();
        return query.Where(entity => entity.ProjectId.HasValue && idList.Contains(entity.ProjectId.Value));
    }
}
