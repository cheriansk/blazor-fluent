using BlazorEnterprise.Shared.DataListTypes;

namespace BlazorEnterprise.Core.Models;

/// <summary>
/// Summary DTO representing a scenario revision.
/// </summary>
public class RevisionSummaryDto
{
    public Guid Id { get; set; }
    public TaskLevelEnum ScopeLevel { get; set; }
    public Guid? ProgramId { get; set; }
    public Guid? ProjectId { get; set; }
    public VersionedFeatureTypeEnum FeatureType { get; set; }
    public int VerNum { get; set; }
    public int ParentVerNum { get; set; } = -1;
    public Guid ParentRevisionHeaderId { get; set; } = Guid.Empty;
    public string RevisionName { get; set; } = string.Empty;
    public RevisionStatusEnum Status { get; set; }
    public string CreatorEmail { get; set; } = string.Empty;
    public string? AssignedToEmails { get; set; }
    public string? ReviewerEmails { get; set; }
    public string? ReviewComments { get; set; }
    public DateTime Created { get; set; }
    public DateTime? SubmittedForReviewAtUtc { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ActivatedAtUtc { get; set; }
    public string? ActivatedBy { get; set; }
    public DateTime? SupersededAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string? CancellationReason { get; set; }
    public int RecordCount { get; set; }
}

/// <summary>
/// Result of comparing two revisions (e.g. Draft vs Active or Draft A vs Draft B).
/// Matched by lineage RelId.
/// </summary>
public class RevisionComparisonDto
{
    public Guid LeftRevisionId { get; set; }
    public int LeftVerNum { get; set; }
    public string LeftRevisionName { get; set; } = string.Empty;
    public RevisionStatusEnum LeftStatus { get; set; }

    public Guid RightRevisionId { get; set; }
    public int RightVerNum { get; set; }
    public string RightRevisionName { get; set; } = string.Empty;
    public RevisionStatusEnum RightStatus { get; set; }

    public List<RevisionRowDiffDto> AddedRows { get; set; } = new();
    public List<RevisionRowDiffDto> RemovedRows { get; set; } = new();
    public List<RevisionRowDiffDto> ModifiedRows { get; set; } = new();
    public List<RevisionRowDiffDto> UnchangedRows { get; set; } = new();
}

/// <summary>
/// Row-level diff within a revision comparison.
/// </summary>
public class RevisionRowDiffDto
{
    public Guid RelId { get; set; }
    public string Title { get; set; } = string.Empty;
    public CalendarEntryTypeEnum EntryType { get; set; }
    public CalendarEntryStatusEnum Status { get; set; }
    public DateTime? LeftStartDate { get; set; }
    public DateTime? LeftEndDate { get; set; }
    public DateTime? RightStartDate { get; set; }
    public DateTime? RightEndDate { get; set; }
    public List<string> Changes { get; set; } = new();
}
