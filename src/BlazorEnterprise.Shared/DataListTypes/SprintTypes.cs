using System.ComponentModel.DataAnnotations;
using BlazorEnterprise.Shared.Attributes;

namespace BlazorEnterprise.Shared.DataListTypes;

/// <summary>
/// Status of an agile sprint iteration.
/// </summary>
[LifecycleEnum]
public enum SprintStatusEnum
{
    [Display(Name = "Planned", Description = "Scheduled future sprint iteration")]
    Planned = 1,

    [Display(Name = "Active", Description = "Currently ongoing active sprint iteration")]
    Active = 2,

    [Display(Name = "Completed", Description = "Past completed sprint iteration; locked from further task assignments")]
    Completed = 3
}

public static class SprintStatusEnumDefinitions
{
    public static readonly IReadOnlyDictionary<SprintStatusEnum, IReadOnlySet<SprintStatusEnum>> AllowedTransitions =
        new Dictionary<SprintStatusEnum, IReadOnlySet<SprintStatusEnum>>
        {
            [SprintStatusEnum.Planned] = new HashSet<SprintStatusEnum> { SprintStatusEnum.Active, SprintStatusEnum.Completed },
            [SprintStatusEnum.Active] = new HashSet<SprintStatusEnum> { SprintStatusEnum.Completed },
            [SprintStatusEnum.Completed] = new HashSet<SprintStatusEnum>()
        };
}
