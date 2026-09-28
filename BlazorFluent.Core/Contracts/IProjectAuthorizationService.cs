using BlazorFluent.Core.Common;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Tenancy;

namespace BlazorFluent.Core.Contracts;

/// <summary>
/// Core contract for Per-Project Role-Based Access Control (RBAC).
/// Manages project member roles, evaluates permissions at runtime,
/// and enforces backend defense-in-depth security gates.
/// </summary>
public interface IProjectAuthorizationService
{
    /// <summary>
    /// Retrieves the active user's assigned role in the specified project.
    /// Returns null if the user has no role in the project.
    /// </summary>
    Task<ProjectRole?> GetCurrentUserProjectRoleAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Checks whether the active user has at least the required role to view/visit the project.
    /// </summary>
    Task<bool> CanVisitAsync(Guid projectId, ProjectRole minRole = ProjectRole.ReadOnly, CancellationToken ct = default);

    /// <summary>
    /// Checks whether the active user has at least the required role to edit/mutate project data.
    /// </summary>
    Task<bool> CanEditAsync(Guid projectId, ProjectRole minRole = ProjectRole.Dev, CancellationToken ct = default);

    /// <summary>
    /// Defense-in-depth gate for read operations.
    /// Throws <see cref="UnauthorizedAccessException"/> and records a forensic security audit event if unauthorized.
    /// </summary>
    Task EnsureCanVisitAsync(Guid projectId, ProjectRole minRole = ProjectRole.ReadOnly, string operation = "", CancellationToken ct = default);

    /// <summary>
    /// Defense-in-depth gate for write operations.
    /// Throws <see cref="UnauthorizedAccessException"/> and records a forensic security audit event if unauthorized.
    /// </summary>
    Task EnsureCanEditAsync(Guid projectId, ProjectRole minRole = ProjectRole.Dev, string operation = "", CancellationToken ct = default);

    /// <summary>
    /// Returns all project IDs in the active tenant where the current user meets or exceeds the required role.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetAuthorizedProjectIdsAsync(ProjectRole minRole = ProjectRole.ReadOnly, CancellationToken ct = default);

    /// <summary>
    /// Lists all members and assigned roles for a project.
    /// </summary>
    Task<IReadOnlyList<ProjectUserRoleEntity>> GetProjectMembersAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Assigns or updates a user's role on a project and invalidates the cached role.
    /// </summary>
    Task<Result> AssignUserRoleAsync(
        Guid projectId,
        string userId,
        string userEmail,
        string userName,
        UserType userType,
        ProjectRole role,
        CancellationToken ct = default);

    /// <summary>
    /// Revokes a user's access from a project and invalidates the cached role.
    /// </summary>
    Task<Result> RevokeUserRoleAsync(Guid projectId, string userId, CancellationToken ct = default);
}
