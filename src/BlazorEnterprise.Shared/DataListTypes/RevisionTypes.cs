using System.ComponentModel.DataAnnotations;
using BlazorEnterprise.Shared.Attributes;

namespace BlazorEnterprise.Shared.DataListTypes;

/// <summary>
/// Status lifecycle of a versioned scenario revision.
/// Multi-draft allowed; strictly one Active revision at any given time per scenario.
/// </summary>
[LifecycleEnum]
public enum RevisionStatusEnum
{
    [Display(Name = "Draft", Description = "Working draft open for collaborative editing")]
    Draft = 1,

    [Display(Name = "Ready for Review", Description = "Submitted and assigned to reviewers for sign-off")]
    ReadyForReview = 2,

    [Display(Name = "Approved", Description = "Approved by reviewers and assigned back to creator for activation")]
    Approved = 3,

    [Display(Name = "Active", Description = "Single effective baseline governing live operations")]
    Active = 4,

    [Display(Name = "Superseded", Description = "Previously active baseline replaced by a newer active version")]
    Superseded = 5,

    [Display(Name = "Cancelled", Description = "Abandoned or automatically cancelled when another version was activated")]
    Cancelled = 6
}

public static class RevisionStatusEnumDefinitions
{
    public static readonly IReadOnlyDictionary<RevisionStatusEnum, IReadOnlySet<RevisionStatusEnum>> AllowedTransitions =
        new Dictionary<RevisionStatusEnum, IReadOnlySet<RevisionStatusEnum>>
        {
            [RevisionStatusEnum.Draft] = new HashSet<RevisionStatusEnum>
            {
                RevisionStatusEnum.ReadyForReview,
                RevisionStatusEnum.Active, // Wizard fast-track
                RevisionStatusEnum.Cancelled
            },
            [RevisionStatusEnum.ReadyForReview] = new HashSet<RevisionStatusEnum>
            {
                RevisionStatusEnum.Approved,
                RevisionStatusEnum.Draft, // Reviewer Rejected -> Reverts to Draft
                RevisionStatusEnum.Cancelled
            },
            [RevisionStatusEnum.Approved] = new HashSet<RevisionStatusEnum>
            {
                RevisionStatusEnum.Active,
                RevisionStatusEnum.Draft,
                RevisionStatusEnum.Cancelled
            },
            [RevisionStatusEnum.Active] = new HashSet<RevisionStatusEnum>
            {
                RevisionStatusEnum.Superseded
            },
            [RevisionStatusEnum.Superseded] = new HashSet<RevisionStatusEnum>(),
            [RevisionStatusEnum.Cancelled] = new HashSet<RevisionStatusEnum>()
        };
}

/// <summary>
/// Feature areas in the enterprise system that support multi-draft scenario versioning.
/// </summary>
[ClassificationEnum]
public enum VersionedFeatureTypeEnum
{
    ProgramCalendar = 1,
    ProjectCalendar = 2,
    SprintPlan = 3,
    Estimation = 4,
    Pricing = 5
}
