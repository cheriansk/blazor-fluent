using BlazorFluent.Core.Attributes;
using System.ComponentModel.DataAnnotations;

namespace BlazorFluent.Core.DataListTypes;

/// <summary>
/// Lightweight, extensible project-level role enumeration.
/// Controls access and edit permissions on project-scoped resources.
/// </summary>
public enum ProjectRole
{
    [Display(Name = "Read Only", Description = "View-only access to project resources")]
    [DataListCategory(ProjectRoleDefinitions.Categories.Standard)]
    ReadOnly = 1,

    [Display(Name = "Quality Assurance", Description = "Quality assurance and testing access")]
    [DataListCategory(ProjectRoleDefinitions.Categories.Technical)]
    QA = 2,

    [Display(Name = "Developer", Description = "Development and engineering write access")]
    [DataListCategory(ProjectRoleDefinitions.Categories.Technical)]
    Dev = 3,

    [Display(Name = "Project Administrator", Description = "Full administrative and membership control")]
    [DataListCategory(ProjectRoleDefinitions.Categories.Administrative)]
    [DataListFilterCriterias(ProjectRoleDefinitions.Filters.ProjectAdmin, ProjectRoleDefinitions.Filters.SuperAdmin)]
    Admin = 4
}

public static class ProjectRoleDefinitions
{
    public static class Categories
    {
        [Display(Name = "Standard Membership", Description = "General non-engineering project roles")]
        public const string Standard = "Standard";

        [Display(Name = "Engineering & Testing", Description = "Technical write access and testing roles")]
        public const string Technical = "Technical";

        [Display(Name = "Administration", Description = "Project management and role governance")]
        public const string Administrative = "Administrative";
    }

    public static class Filters
    {
        [Display(Name = "Project Admin Context", Description = "Visible only to project administrators")]
        public const string ProjectAdmin = "ProjectAdmin";

        [Display(Name = "SuperAdmin Required", Description = "Requires SuperAdmin privileges")]
        public const string SuperAdmin = "SuperAdmin";
    }
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
