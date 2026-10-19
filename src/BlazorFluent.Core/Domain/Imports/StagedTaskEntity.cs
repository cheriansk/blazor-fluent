using BlazorFluent.Core.Domain.Base;

namespace BlazorFluent.Core.Domain.Imports;

/// <summary>
/// Intermediate staging entity representing an uncommitted project task parsed from an import file.
/// Isolated per tenant and per project in schema 'staging.StagedTasks'.
/// Validated rows are staged here before final transaction commit into 'tasks.Tasks'.
/// </summary>
public class StagedTaskEntity : TenantAuditableEntity, IProjectScopedEntity, IHardDeletableEntity
{
    /// <summary>
    /// Foreign key link to the parent ImportFileEntity.
    /// </summary>
    public required Guid ImportFileId { get; set; }

    /// <summary>
    /// Foreign batch identifier grouping all files uploaded together in the same session.
    /// </summary>
    public required Guid ImportId { get; set; }

    /// <summary>
    /// ID of the project workspace.
    /// </summary>
    public required Guid ProjectId { get; set; }

    /// <summary>
    /// Originating sheet or section name (e.g., 'Tasks' for Excel).
    /// </summary>
    public required string SheetName { get; set; }

    /// <summary>
    /// 1-indexed row number within the source file.
    /// </summary>
    public int RowIndex { get; set; }

    /// <summary>
    /// Parsed title of the task.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Parsed task description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Parsed priority (Low, Medium, High, Urgent).
    /// </summary>
    public required string Priority { get; set; }

    /// <summary>
    /// Parsed status (Open, InProgress, Closed).
    /// </summary>
    public required string Status { get; set; }

    /// <summary>
    /// Parsed due date deadline.
    /// </summary>
    public DateTime? DueDate { get; set; }

    /// <summary>
    /// Semicolon-delimited list of assignee emails.
    /// </summary>
    public string? AssigneeEmails { get; set; }

    /// <summary>
    /// Comma-delimited list of task labels.
    /// </summary>
    public string? Labels { get; set; }

    /// <summary>
    /// Validation result for this specific row ("Valid", "Invalid").
    /// </summary>
    public required string ValidationStatus { get; set; }

    /// <summary>
    /// Specific cell or row error description if row failed validation.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
