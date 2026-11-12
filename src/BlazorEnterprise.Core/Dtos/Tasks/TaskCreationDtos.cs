using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using BlazorEnterprise.Shared.Attributes;
using BlazorEnterprise.Shared.DataListTypes;

namespace BlazorEnterprise.Core.Dtos.Tasks;

/// <summary>
/// Subtype discriminator for creating tasks through the unified TaskCreationDialog.
/// </summary>
[ClassificationEnum]
public enum CreationTaskTypeEnum
{
    [Display(Name = "Sprint Task", Description = "Task assigned directly to a sprint iteration")]
    SprintTask = 1,

    [Display(Name = "Misc Task", Description = "Ad-hoc or backlog task")]
    MiscTask = 2,

    [Display(Name = "Risk", Description = "Formal enterprise RAID risk")]
    Risk = 3,

    [Display(Name = "Assumption", Description = "Operational or strategic planning assumption")]
    Assumption = 4,

    [Display(Name = "Issue", Description = "Realized impediment or delivery blocker")]
    Issue = 5
}

/// <summary>
/// Unified request DTO for creating any task subtype.
/// </summary>
public class TaskCreationRequestDto
{
    public CreationTaskTypeEnum TaskType { get; set; }

    // Scope & Tenancy
    public Guid TenantId { get; set; }
    public Guid? ProgramId { get; set; }
    public Guid? ProjectId { get; set; }

    // Common BaseTaskEntity Fields
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskPriorityEnum Priority { get; set; }
    public TaskStatusEnum Status { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string? AssigneeEmails { get; set; }
    public decimal? EstimatedHours { get; set; }
    public string? Labels { get; set; }
    public Guid? MilestoneId { get; set; }

    // Sprint Task Specific
    public Guid? SprintId { get; set; }
    public double? StoryPoints { get; set; }
    public string? AcceptanceCriteria { get; set; }

    // Misc Task Specific
    public string? MiscCategory { get; set; }
    public string? RelatedLink { get; set; }
    public string? Recurrence { get; set; }
    public DateTime? RecurrenceEndDate { get; set; }

    // Risk Specific
    public string? RiskCategory { get; set; }
    public RiskProbabilityEnum Probability { get; set; }
    public RiskImpactEnum Impact { get; set; }
    public string? MitigationStrategy { get; set; }
    public string? ContingencyPlan { get; set; }
    public string? RiskOwnerEmail { get; set; }

    // Assumption Specific
    public string? AssumptionCategory { get; set; }
    public string? ValidationCriteria { get; set; }
    public DateTime? ValidationDate { get; set; }
    public string? AssumptionOwnerEmail { get; set; }
    public AssumptionValidationStatusEnum ValidationStatus { get; set; }

    // Issue Specific
    public string? IssueCategory { get; set; }
    public IssueSeverityEnum Severity { get; set; }
    public string? RootCause { get; set; }
    public string? ImpactAssessment { get; set; }
    public string? Workaround { get; set; }
    public string? IssueOwnerEmail { get; set; }
    public DateTime? TargetResolutionDate { get; set; }
}

/// <summary>
/// Related data lookup result for populating dropdowns in TaskCreationDialog.
/// </summary>
public class TaskCreationRelatedDataDto
{
    public List<SprintLookupItemDto> Sprints { get; set; } = [];
    public List<TeamMemberLookupItemDto> TeamMembers { get; set; } = [];
    public List<MilestoneLookupItemDto> Milestones { get; set; } = [];
}

public record SprintLookupItemDto(Guid Id, string Name, int Number, bool IsCompleted);
public record TeamMemberLookupItemDto(string Email, string FullName, string Role);
public record MilestoneLookupItemDto(Guid Id, string Title, DateTime? TargetDate);
