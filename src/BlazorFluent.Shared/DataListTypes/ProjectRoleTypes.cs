using BlazorFluent.Shared.Attributes;
using System.ComponentModel.DataAnnotations;

namespace BlazorFluent.Shared.DataListTypes;

/// <summary>
/// Lightweight, extensible project-level role enumeration.
/// Controls access and edit permissions on project-scoped resources.
/// </summary>
public enum ProjectRole
{
    [Display(Name = "ReadOnly Team", Description = "View-only access to project resources")]
    [DataListCategory(ProjectRoleDefinitions.Categories.Standard)]
    ReadOnly = 10,

    [Display(Name = "Contribute Team", Description = "Standard read and write access to tasks and deliverables")]
    [DataListCategory(ProjectRoleDefinitions.Categories.Technical)]
    Contribute = 20,

    [Display(Name = "Lead Team", Description = "Team lead access: approvals, sprint design, and workflows")]
    [DataListCategory(ProjectRoleDefinitions.Categories.Technical)]
    Lead = 30,

    [Display(Name = "ElevatedLead Team", Description = "Senior technical lead access: architecture, security, and risk")]
    [DataListCategory(ProjectRoleDefinitions.Categories.Technical)]
    ElevatedLead = 40,

    [Display(Name = "PMO Team", Description = "Project Management Office: governance, financials, and contracts")]
    [DataListCategory(ProjectRoleDefinitions.Categories.Administrative)]
    PMO = 50,

    [Display(Name = "Admin Team", Description = "Full administrative and membership control")]
    [DataListCategory(ProjectRoleDefinitions.Categories.Administrative)]
    [DataListFilterCriterias(ProjectRoleDefinitions.Filters.ProjectAdmin, ProjectRoleDefinitions.Filters.SuperAdmin)]
    Admin = 100,

    // Backward-compatibility aliases
    Dev = Contribute,
    QA = Contribute
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
    /// Returns true if the role has permission to write/mutate project data (Contribute and above).
    /// </summary>
    public static bool CanWrite(this ProjectRole role) =>
        (int)role >= (int)ProjectRole.Contribute;

    /// <summary>
    /// Returns true if the role has permission to manage project team membership and roles (Admin).
    /// </summary>
    public static bool CanManageAccess(this ProjectRole role) =>
        (int)role >= (int)ProjectRole.Admin;

    /// <summary>
    /// Returns true if the assigned role has equal or higher hierarchy than the required role.
    /// Numeric hierarchy: ReadOnly (10) &lt; Contribute (20) &lt; Lead (30) &lt; ElevatedLead (40) &lt; PMO (50) &lt; Admin (100).
    /// </summary>
    public static bool Satisfies(this ProjectRole role, ProjectRole requiredRole) =>
        (int)role >= (int)requiredRole;
}
