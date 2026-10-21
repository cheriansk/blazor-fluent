using BlazorFluent.Shared.Attributes;
using System.ComponentModel.DataAnnotations;

namespace BlazorFluent.Shared.DataListTypes;

/// <summary>
/// Lifecycle status of a project workspace.
/// </summary>
[LifecycleEnum]
public enum ProjectStatusEnum
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

public static class ProjectStatusEnumDefinitions
{
    public static readonly IReadOnlyDictionary<ProjectStatusEnum, IReadOnlySet<ProjectStatusEnum>> AllowedTransitions =
        new Dictionary<ProjectStatusEnum, IReadOnlySet<ProjectStatusEnum>>
        {
            [ProjectStatusEnum.New] = new HashSet<ProjectStatusEnum> { ProjectStatusEnum.Active, ProjectStatusEnum.OnHold, ProjectStatusEnum.Done },
            [ProjectStatusEnum.Active] = new HashSet<ProjectStatusEnum> { ProjectStatusEnum.OnHold, ProjectStatusEnum.Done },
            [ProjectStatusEnum.OnHold] = new HashSet<ProjectStatusEnum> { ProjectStatusEnum.Active, ProjectStatusEnum.Done },
            [ProjectStatusEnum.Done] = new HashSet<ProjectStatusEnum> { ProjectStatusEnum.Active }
        };
}
