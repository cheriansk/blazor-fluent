using System.ComponentModel.DataAnnotations;

namespace BlazorFluent.Core.DataListTypes;

/// <summary>
/// Lifecycle status of a project workspace.
/// </summary>
public enum ProjectStatus
{
    [Display(Name = "New", Description = "Project has been created and is pending kickoff")]
    New = 1,

    [Display(Name = "Active", Description = "Project is actively in progress")]
    Active = 2,

    [Display(Name = "On Hold", Description = "Project is temporarily suspended")]
    OnHold = 3,

    [Display(Name = "Done", Description = "Project has been completed")]
    Done = 4
}
