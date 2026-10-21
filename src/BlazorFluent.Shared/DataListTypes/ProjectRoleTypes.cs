using BlazorFluent.Shared.Attributes;
using System.ComponentModel.DataAnnotations;

namespace BlazorFluent.Shared.DataListTypes;

/// <summary>
/// Lightweight, extensible project-level role enumeration.
/// Controls access and edit permissions on project-scoped resources.
/// </summary>
[ClassificationEnum]
public enum ProjectRoleEnum
{
    [Display(Name = "ReadOnly Team", Description = "View-only access to project resources")]
    [DataListCategory(ProjectRoleEnumDefinitions.Categories.Standard)]
    ReadOnly = 10,

    [Display(Name = "Contribute Team", Description = "Standard read and write access to tasks and deliverables")]
    [DataListCategory(ProjectRoleEnumDefinitions.Categories.Technical)]
    Contribute = 20,

    [Display(Name = "Lead Team", Description = "Team lead access: approvals, sprint design, and workflows")]
    [DataListCategory(ProjectRoleEnumDefinitions.Categories.Technical)]
    Lead = 30,

    [Display(Name = "ElevatedLead Team", Description = "Senior technical lead access: architecture, security, and risk")]
    [DataListCategory(ProjectRoleEnumDefinitions.Categories.Technical)]
    ElevatedLead = 40,

    [Display(Name = "PMO Team", Description = "Project Management Office: governance, financials, and contracts")]
    [DataListCategory(ProjectRoleEnumDefinitions.Categories.Administrative)]
    PMO = 50,

    [Display(Name = "Admin Team", Description = "Full administrative and membership control")]
    [DataListCategory(ProjectRoleEnumDefinitions.Categories.Administrative)]
    [DataListFilterCriterias(ProjectRoleEnumDefinitions.Filters.ProjectAdmin, ProjectRoleEnumDefinitions.Filters.SuperAdmin)]
    Admin = 100,
}

public static class ProjectRoleEnumDefinitions
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
    public static bool CanWrite(this ProjectRoleEnum role) =>
        (int)role >= (int)ProjectRoleEnum.Contribute;

    /// <summary>
    /// Returns true if the role has permission to manage project team membership and roles (Admin).
    /// </summary>
    public static bool CanManageAccess(this ProjectRoleEnum role) =>
        (int)role >= (int)ProjectRoleEnum.Admin;

    /// <summary>
    /// Returns true if the assigned role has equal or higher hierarchy than the required role.
    /// Numeric hierarchy: ReadOnly (10) &lt; Contribute (20) &lt; Lead (30) &lt; ElevatedLead (40) &lt; PMO (50) &lt; Admin (100).
    /// </summary>
    public static bool Satisfies(this ProjectRoleEnum role, ProjectRoleEnum requiredRole) =>
        (int)role >= (int)requiredRole;
}
