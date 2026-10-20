using BlazorFluent.Shared.DataListTypes;

namespace BlazorFluent.Core.Dtos;

/// <summary>
/// Definition of an initial child subtask under a workflow step, delegated to a team member.
/// </summary>
public record CreateWorkflowSubTaskDto
{
    public string Title { get; set; } = string.Empty;
    public string AssigneeEmail { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; } = DateTime.UtcNow.AddDays(3);
}

/// <summary>
/// Definition of a single step (Task) to be created or attached within an enterprise sequential workflow.
/// If ExistingTaskId is set, attaches the existing task; otherwise creates a new TaskEntity.
/// </summary>
public record CreateWorkflowStepDto
{
    public Guid? ExistingTaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string StepOwnerEmail { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; } = DateTime.UtcNow.AddDays(3);
    public List<CreateWorkflowSubTaskDto> SubTasks { get; set; } = new();
}

/// <summary>
/// Payload to create and launch a new sequential workflow at Tenant, Program, or Project level.
/// </summary>
public record CreateWorkflowReqDto
{
    public TaskLevelEnum Level { get; set; } = TaskLevelEnum.Project;
    public Guid? ProgramId { get; set; }
    public Guid? ProjectId { get; set; }
    public string WorkflowOwnerEmail { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TaskPriorityEnum Priority { get; set; } = TaskPriorityEnum.Medium;
    public DateTime? DueDate { get; set; } = DateTime.UtcNow.AddDays(14);
    public string WatcherEmails { get; set; } = string.Empty;
    public List<CreateWorkflowStepDto> Steps { get; set; } = new();
}

/// <summary>
/// Payload submitted by the verified active step owner or workflow owner to advance the pipeline.
/// </summary>
public record CompleteWorkflowStepReqDto(
    Guid WorkflowId,
    Guid StepId,
    string CompletionNotes
);

/// <summary>
/// Payload submitted by the step owner, workflow owner, or an admin to reassign an active step.
/// </summary>
public record ReassignWorkflowStepReqDto(
    Guid WorkflowId,
    Guid StepId,
    string NewOwnerEmail,
    string Reason
);

/// <summary>
/// Payload to add a subtask to an existing workflow step and delegate it to a team member.
/// </summary>
public record AddSubTaskToStepReqDto(
    Guid WorkflowId,
    Guid StepTaskId,
    string Title,
    string AssigneeEmail,
    DateTime? DueDate
);

/// <summary>
/// Payload to toggle the completion status of a subtask under a workflow step.
/// </summary>
public record ToggleSubTaskStatusReqDto(
    Guid SubTaskId,
    bool IsClosed
);

/// <summary>
/// Filter criteria for querying workflows across Tenant, Program, and Project levels.
/// </summary>
public record WorkflowFilterDto(
    TaskLevelEnum? Level = null,
    Guid? ProgramId = null,
    Guid? ProjectId = null,
    string? SearchTerm = null,
    bool? IsCompleted = null
);

/// <summary>
/// Payload to update workflow metadata (title, description, owner, due date, priority, watchers).
/// </summary>
public record EditWorkflowReqDto
{
    public Guid WorkflowId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string WorkflowOwnerEmail { get; set; } = string.Empty;
    public TaskPriorityEnum Priority { get; set; } = TaskPriorityEnum.Medium;
    public DateTime? DueDate { get; set; }
    public string WatcherEmails { get; set; } = string.Empty;
}

/// <summary>
/// Payload to cancel an existing workflow step. Cancellation can only be performed by the respective Admin.
/// If the step was active, workflow enters Paused status.
/// </summary>
public record CancelWorkflowStepReqDto(
    Guid WorkflowId,
    Guid StepTaskId,
    string CancellationReason
);

/// <summary>
/// Payload for Admin to resume a paused workflow after step cancellation, optionally specifying which step to activate.
/// </summary>
public record ResumePausedWorkflowReqDto(
    Guid WorkflowId,
    Guid? NextStepTaskId = null
);

/// <summary>
/// Payload to append or insert a task (existing or new) into an in-flight workflow sequence.
/// </summary>
public record AppendStepToWorkflowReqDto(
    Guid WorkflowId,
    Guid? ExistingTaskId,
    string? NewTaskTitle,
    string? NewTaskOwnerEmail,
    string? DeliverableNotes,
    int? InsertAfterStepOrder = null
);

/// <summary>
/// Filter criteria for finding eligible existing tasks to attach to a workflow.
/// </summary>
public record TaskSearchFilterDto(
    TaskLevelEnum Level,
    Guid? ProgramId = null,
    Guid? ProjectId = null,
    string? SearchTerm = null,
    bool ExcludeTasksWithWorkflows = true
);
