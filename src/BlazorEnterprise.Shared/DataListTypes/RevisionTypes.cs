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
    Cancelled = 6,

    [Display(Name = "Complete", Description = "Universal terminal state when an active revision scenario or cycle is finalized and closed.")]
    Complete = 7,

    [Display(Name = "Draft Complete", Description = "Sprint-exclusive terminal state capturing the final live working draft upon sprint completion.")]
    DraftComplete = 8
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
                RevisionStatusEnum.Superseded,
                RevisionStatusEnum.Complete
            },
            [RevisionStatusEnum.Superseded] = new HashSet<RevisionStatusEnum>(),
            [RevisionStatusEnum.Cancelled] = new HashSet<RevisionStatusEnum>(),
            [RevisionStatusEnum.Complete] = new HashSet<RevisionStatusEnum>(),
            [RevisionStatusEnum.DraftComplete] = new HashSet<RevisionStatusEnum>()
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

/// <summary>
/// Operational versioning policy governing how revisions and drafts behave for a feature area.
/// </summary>
[ClassificationEnum]
public enum VersioningModeEnum
{
    /// <summary>
    /// Multi-draft scenario modeling: users can explicitly create multiple concurrent drafts and review cycles.
    /// </summary>
    MultiDraft = 1,

    /// <summary>
    /// Single-version Lock-and-Auto-Draft (e.g., Sprint Planning, Baseline vs Live Working Copy).
    /// Finalizing a baseline revision locks it and immediately auto-spawns a single working draft.
    /// Explicit creation of arbitrary drafts is blocked.
    /// </summary>
    LockAndAutoDraft = 2
}

public static class VersionedFeatureTypeEnumDefinitions
{
    public static bool IsSprintExclusiveStatus(RevisionStatusEnum status) =>
        status == RevisionStatusEnum.DraftComplete;

    public static VersioningModeEnum GetVersioningMode(VersionedFeatureTypeEnum featureType) => featureType switch
    {
        VersionedFeatureTypeEnum.SprintPlan => VersioningModeEnum.LockAndAutoDraft,
        _ => VersioningModeEnum.MultiDraft
    };
}