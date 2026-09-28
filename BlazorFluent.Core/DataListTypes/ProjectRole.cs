namespace BlazorFluent.Core.DataListTypes;

/// <summary>
/// Lightweight, extensible project-level role enumeration.
/// Controls access and edit permissions on project-scoped resources.
/// </summary>
public enum ProjectRole
{
    ReadOnly = 1,
    QA = 2,
    Dev = 3,
    Admin = 4
}

public static class ProjectRoleExtensions
{
    /// <summary>
    /// Returns true if the role has permission to write/mutate project data (Dev, QA, Admin).
    /// </summary>
    public static bool CanWrite(this ProjectRole role) =>
        role is ProjectRole.Dev or ProjectRole.QA or ProjectRole.Admin;

    /// <summary>
    /// Returns true if the role has permission to manage project team membership and roles (Admin).
    /// </summary>
    public static bool CanManageAccess(this ProjectRole role) =>
        role is ProjectRole.Admin;

    /// <summary>
    /// Returns true if the assigned role has equal or higher hierarchy than the required role.
    /// Hierarchy order: ReadOnly (1) &lt; QA (2) &lt; Dev (3) &lt; Admin (4).
    /// </summary>
    public static bool Satisfies(this ProjectRole role, ProjectRole requiredRole) =>
        (int)role >= (int)requiredRole;
}
