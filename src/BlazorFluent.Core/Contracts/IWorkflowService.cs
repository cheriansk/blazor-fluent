using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Core.Domain.Tasks;
using BlazorFluent.Core.Dtos;

namespace BlazorFluent.Core.Contracts;

/// <summary>
/// Domain service interface for orchestrating sequential, multi-level workflow pipelines.
/// Governed by a constant Workflow Owner who maintains overarching accountability,
/// with discrete steps modeled as TaskEntity instances and child SubTaskEntity items.
/// Enforces Zero Trust caller authorization and automated multi-channel notification dispatch.
/// </summary>
public interface IWorkflowService
{
    /// <summary>
    /// Creates and launches a new sequential workflow with ordered steps (TaskEntity)
    /// and optional initial subtasks (SubTaskEntity) assigned to team members.
    /// Supports Tenant, Program, or Project level workflows.
    /// Sets Step 1 to Active and assigns initial active ownership to Step 1's owner,
    /// while the designated Workflow Owner remains constant.
    /// </summary>
    Task<WorkflowEntity> CreateWorkflowAsync(CreateWorkflowReqDto request, CancellationToken ct = default);

    /// <summary>
    /// Completes the currently active step and advances the workflow to the subsequent step.
    /// Zero Trust gate: Caller must be the verified StepOwner of the current active step,
    /// the constant Workflow Owner, or a Tenant/Root Admin.
    /// Fired notifications: Next step owner (Action Required alert), Workflow Owner and Watchers (Progress milestone alert).
    /// </summary>
    Task<WorkflowEntity> CompleteStepAsync(CompleteWorkflowStepReqDto request, CancellationToken ct = default);

    /// <summary>
    /// Reassigns an active or pending step to a new designated step owner with mandatory audit logging.
    /// Zero Trust gate: Caller must be the current StepOwner, the constant Workflow Owner, or an Admin.
    /// </summary>
    Task<WorkflowEntity> ReassignStepAsync(ReassignWorkflowStepReqDto request, CancellationToken ct = default);

    /// <summary>
    /// Delegates a child subtask under a workflow step to an individual team member.
    /// Zero Trust gate: Caller must be the StepOwner, the Workflow Owner, or an Admin.
    /// </summary>
    Task<SubTaskEntity> AddSubTaskToStepAsync(AddSubTaskToStepReqDto request, CancellationToken ct = default);

    /// <summary>
    /// Toggles the completion status of a subtask under a workflow step.
    /// Zero Trust gate: Caller must be the assigned team member, Step Owner, Workflow Owner, or Admin.
    /// </summary>
    Task<SubTaskEntity> ToggleSubTaskStatusAsync(ToggleSubTaskStatusReqDto request, CancellationToken ct = default);

    /// <summary>
    /// Retrieves workflows filtered by scope (Tenant, Program, Project), status, and search query.
    /// Includes ordered steps and each step's delegated subtasks.
    /// </summary>
    Task<IReadOnlyList<WorkflowEntity>> GetWorkflowsAsync(WorkflowFilterDto filter, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all workflows associated with a given project, including ordered child steps and subtasks.
    /// </summary>
    Task<IReadOnlyList<WorkflowEntity>> GetProjectWorkflowsAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a single workflow by ID, including its ordered child steps and subtasks.
    /// </summary>
    Task<WorkflowEntity?> GetWorkflowByIdAsync(Guid workflowId, CancellationToken ct = default);

    /// <summary>
    /// Updates workflow metadata (title, description, owner, due date, priority, watchers).
    /// Zero Trust gate: Caller must be the designated Admin for the workflow scope (Tenant, Program, Project).
    /// </summary>
    Task<WorkflowEntity> EditWorkflowAsync(EditWorkflowReqDto request, CancellationToken ct = default);

    /// <summary>
    /// Cancels a step within a workflow with a mandatory reason. Soft cancellation (not deleted).
    /// If the step was active, workflow transitions to Paused status for Admin review.
    /// Zero Trust gate: Caller must be the designated Admin for the workflow scope.
    /// </summary>
    Task<WorkflowEntity> CancelStepAsync(CancelWorkflowStepReqDto request, CancellationToken ct = default);

    /// <summary>
    /// Resumes a paused workflow following step cancellation, activating the next sequential step or specified step.
    /// Zero Trust gate: Caller must be the designated Admin for the workflow scope.
    /// </summary>
    Task<WorkflowEntity> ResumePausedWorkflowAsync(ResumePausedWorkflowReqDto request, CancellationToken ct = default);

    /// <summary>
    /// Appends or inserts a task (existing or newly authored) into an in-flight workflow sequence.
    /// Zero Trust gate: Caller must be the designated Admin for the workflow scope.
    /// </summary>
    Task<WorkflowEntity> AppendStepAsync(AppendStepToWorkflowReqDto request, CancellationToken ct = default);

    /// <summary>
    /// Retrieves eligible existing tasks that match the specified scope and can be attached to a workflow.
    /// By default excludes tasks already attached to active workflows.
    /// </summary>
    Task<IReadOnlyList<TaskEntity>> GetAvailableTasksForWorkflowAsync(TaskSearchFilterDto filter, CancellationToken ct = default);

    /// <summary>
    /// Evaluates whether the current caller is authorized to design, edit, or cancel workflows at the given scope.
    /// </summary>
    Task<bool> CanUserManageWorkflowAsync(TaskLevelEnum level, Guid? programId, Guid? projectId, CancellationToken ct = default);
}
