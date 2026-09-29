using BlazorFluent.Core.Common;
using BlazorFluent.Core.Domain.Tasks;

namespace BlazorFluent.Core.Contracts;

/// <summary>
/// Domain service contract managing project work items, swimlane task lifecycle, and Jira-style comments.
/// </summary>
public interface IUserTaskService
{
    /// <summary>
    /// Retrieves all active (non-deleted) tasks for a project, ordered by DueDate ascending.
    /// </summary>
    Task<IReadOnlyList<UserTaskEntity>> GetTasksByProjectAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a single task by ID including its discussion comments.
    /// </summary>
    Task<UserTaskEntity?> GetTaskByIdAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>
    /// Creates a new project task, stamps audit context, and dispatches automated in-app notifications to all assignees.
    /// </summary>
    Task<Result<UserTaskEntity>> CreateTaskAsync(UserTaskEntity task, CancellationToken ct = default);

    /// <summary>
    /// Updates details, priority, due date, assignees, or labels of an existing task.
    /// </summary>
    Task<Result<UserTaskEntity>> UpdateTaskAsync(UserTaskEntity task, CancellationToken ct = default);

    /// <summary>
    /// Marks a task as Closed with completion timestamp and user identifier.
    /// </summary>
    Task<Result<bool>> CloseTaskAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>
    /// Reopens a previously closed task, setting Status back to Open.
    /// </summary>
    Task<Result<bool>> ReopenTaskAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>
    /// Appends a new chronological comment to the task discussion thread.
    /// </summary>
    Task<Result<UserTaskCommentEntity>> AddCommentAsync(Guid taskId, string commentText, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all chronological comments for a task.
    /// </summary>
    Task<IReadOnlyList<UserTaskCommentEntity>> GetCommentsAsync(Guid taskId, CancellationToken ct = default);
}
