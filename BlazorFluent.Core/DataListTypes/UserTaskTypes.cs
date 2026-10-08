using BlazorFluent.Core.Attributes;
using System.ComponentModel.DataAnnotations;

namespace BlazorFluent.Core.DataListTypes;

#region TaskPriority Definitions

/// <summary>
/// Static definitions for TaskPriority category and search filter codes.
/// </summary>
public static class UserTaskPriorityDefinitions
{
    public static class Categories
    {
        public const string Standard = "Standard";
        public const string Escalated = "Escalated";

        [Display(Name = "Standard Priority", Description = "Routine and medium priority work items.")]
        public const string StandardDisplay = Standard;

        [Display(Name = "Escalated Priority", Description = "Urgent or time-critical work items requiring expedited attention.")]
        public const string EscalatedDisplay = Escalated;
    }

    public static class Filters
    {
        public const string Routine = "Routine";
        public const string Urgent = "Urgent";

        [Display(Name = "Routine Items", Description = "Low and medium priority tasks.")]
        public const string RoutineDisplay = Routine;

        [Display(Name = "Urgent Items", Description = "High and urgent priority tasks.")]
        public const string UrgentDisplay = Urgent;
    }
}

/// <summary>
/// Urgency and priority rating for project tasks.
/// </summary>
public enum UserTaskPriority
{
    [Display(Name = "Low", Description = "Minor task or non-blocking improvement.")]
    [DataListCategory(UserTaskPriorityDefinitions.Categories.Standard)]
    [DataListFilterCriterias(UserTaskPriorityDefinitions.Filters.Routine)]
    Low = 1,

    [Display(Name = "Medium", Description = "Normal operational work item.")]
    [DataListCategory(UserTaskPriorityDefinitions.Categories.Standard)]
    [DataListFilterCriterias(UserTaskPriorityDefinitions.Filters.Routine)]
    Medium = 2,

    [Display(Name = "High", Description = "Important task with high business priority.")]
    [DataListCategory(UserTaskPriorityDefinitions.Categories.Escalated)]
    [DataListFilterCriterias(UserTaskPriorityDefinitions.Filters.Urgent)]
    High = 3,

    [Display(Name = "Urgent", Description = "Critical blocker requiring immediate resolution.")]
    [DataListCategory(UserTaskPriorityDefinitions.Categories.Escalated)]
    [DataListFilterCriterias(UserTaskPriorityDefinitions.Filters.Urgent)]
    Urgent = 4
}

#endregion

#region UserTaskStatus Definitions

/// <summary>
/// Lifecycle status of a project task.
/// </summary>
public enum UserTaskStatus
{
    [Display(Name = "Open", Description = "Task is created and pending initial work.")]
    Open = 1,

    [Display(Name = "In Progress", Description = "Task is actively being worked on.")]
    InProgress = 2,

    [Display(Name = "Closed", Description = "Task has been resolved or completed.")]
    Closed = 3,

    [Display(Name = "Cancelled", Description = "Task has been cancelled or aborted.")]
    Cancelled = 4
}

#endregion
