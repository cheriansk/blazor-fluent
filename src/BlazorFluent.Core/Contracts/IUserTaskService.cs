using BlazorFluent.Core.Domain.Tasks;
using BlazorFluent.Core.Dtos.Response;
using BlazorFluent.Shared.DataListTypes;

namespace BlazorFluent.Core.Contracts;

/// <summary>
/// Domain service contract managing project work items, swimlane task lifecycle, and universal enterprise dependencies.
/// </summary>
public interface IUserTaskService
{
    /// <summary>
    /// Retrieves all active (non-deleted) tasks for a project, ordered by DueDate ascending.
    /// </summary>
    Task<IReadOnlyList<TaskEntity>> GetTasksByProjectAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Streams tasks for a project as an asynchronous sequence for batch export and processing pipelines.
    /// </summary>
    IAsyncEnumerable<TaskEntity> GetTasksByProjectStreamAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a single task by ID within a specific project including its discussion comments.
    /// </summary>
    Task<TaskEntity?> GetTaskByIdAsync(Guid projectId, Guid taskId, CancellationToken ct = default);

    /// <summary>
    /// Creates a new project task, stamps audit context, and dispatches automated in-app notifications to all assignees.
    /// </summary>
    Task<Result<TaskEntity>> CreateTaskAsync(TaskEntity task, CancellationToken ct = default);

    /// <summary>
    /// Updates details, priority, due date, assignees, or labels of an existing task.
    /// </summary>
    Task<Result<TaskEntity>> UpdateTaskAsync(TaskEntity task, CancellationToken ct = default);

    /// <summary>
    /// Marks a task as Closed with completion timestamp and user identifier within a specific project.
    /// </summary>
    Task<Result<bool>> CloseTaskAsync(Guid projectId, Guid taskId, CancellationToken ct = default);

    /// <summary>
    /// Reopens a previously closed task, setting Status back to Open within a specific project.
    /// </summary>
    Task<Result<bool>> ReopenTaskAsync(Guid projectId, Guid taskId, CancellationToken ct = default);

    /// <summary>
    /// Cancels an existing task, setting its Status to Cancelled.
    /// </summary>
    Task<Result<bool>> CancelTaskAsync(Guid projectId, Guid taskId, CancellationToken ct = default);

    /// <summary>
    /// Appends a new chronological comment to the task discussion thread within a specific project.
    /// </summary>
    Task<Result<UserTaskCommentEntity>> AddCommentAsync(Guid projectId, Guid taskId, string commentText, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all chronological comments for a task within a specific project.
    /// </summary>
    Task<IReadOnlyList<UserTaskCommentEntity>> GetCommentsAsync(Guid projectId, Guid taskId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all dependency links (blockers and blocked-by) for a specific task.
    /// </summary>
    Task<IReadOnlyList<TaskDependencyItemDto>> GetTaskDependenciesAsync(Guid projectId, Guid taskId, CancellationToken ct = default);

    /// <summary>
    /// Registers a blocking or prerequisite dependency between two tasks or endpoints.
    /// </summary>
    Task<Result<bool>> AddTaskDependencyAsync(
        Guid projectId,
        Guid taskId,
        Guid dependsOnTaskId,
        TaskDependencyTypeEnum dependencyType = TaskDependencyTypeEnum.Blocks,
        DateTime? resolveByUtc = null,
        string? notes = null,
        CancellationToken ct = default);

    /// <summary>
    /// Removes a task dependency link.
    /// </summary>
    Task<Result<bool>> RemoveTaskDependencyAsync(Guid projectId, Guid dependencyId, CancellationToken ct = default);

    // --- SubTasks Management (Strict Depth-1) ---
    Task<IReadOnlyList<SubTaskEntity>> GetSubTasksByTaskIdAsync(Guid taskId, CancellationToken ct = default);
    Task<Result<SubTaskEntity>> CreateSubTaskAsync(SubTaskEntity subTask, CancellationToken ct = default);
    Task<Result<SubTaskEntity>> UpdateSubTaskAsync(SubTaskEntity subTask, CancellationToken ct = default);
    Task<Result<bool>> DeleteSubTaskAsync(Guid subTaskId, CancellationToken ct = default);

    // --- Enterprise Blocker Dependencies (D of RAID) & Links ---
    Task<IReadOnlyList<WorkDependencyEntity>> GetTaskLinksAsync(Guid taskId, CancellationToken ct = default);

    Task<Result<WorkDependencyEntity>> CreateBlockedByLinkAsync(
        Guid sourceTaskId,
        Guid targetTaskId,
        DateTime resolveDueDate,
        DependencyCriticalityEnum criticality,
        string impactDetails,
        string? watcherEmails = null,
        CancellationToken ct = default);

    Task<Result<bool>> RemoveTaskLinkAsync(Guid linkId, CancellationToken ct = default);

    /// <summary>
    /// Promotes an existing ManualDependency into a linked TaskItem or CalendarGate in-place.
    /// </summary>
    Task<Result<WorkDependencyEntity>> PromoteManualDependencyAsync(
        Guid dependencyId,
        DependencyNodeTypeEnum targetType,
        Guid targetItemId,
        Guid? targetProjectId,
        Guid? targetProgramId,
        CancellationToken ct = default);

    Task<IReadOnlyList<TaskEntity>> GetTenantTasksAsync(Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<RiskEntity>> GetTenantRisksAsync(Guid tenantId, CancellationToken ct = default);
    Task<Result<RiskEntity>> CreateRiskAsync(RiskEntity risk, CancellationToken ct = default);
    Task<IReadOnlyList<TaskEntity>> GetRecentTasksAsync(int limit = 30, CancellationToken ct = default);
    Task<IReadOnlyList<WorkDependencyEntity>> GetProjectWorkDependenciesAsync(Guid projectId, CancellationToken ct = default);
}