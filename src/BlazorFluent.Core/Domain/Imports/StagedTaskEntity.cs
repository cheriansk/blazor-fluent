using BlazorFluent.Core.Domain.Base;

namespace BlazorFluent.Core.Domain.Imports;

/// <summary>
/// Intermediate staging entity representing an uncommitted project task parsed from an import file.
/// Isolated per tenant and per project in schema 'staging.StagedTasks'.
/// Validated rows are staged here before final transaction commit into 'tasks.Tasks'.
/// </summary>
public class StagedTaskEntity : TenantAuditableEntity, IProjectScopedEntity
{
    /// <summary>
    /// Foreign key link to the parent ImportFileEntity.
    /// </summary>
    public Guid ImportFileId { get; set; }

    /// <summary>
    /// Foreign batch identifier grouping all files uploaded together in the same session.
    /// </summary>
    public Guid ImportId { get; set; }

    /// <summary>
    /// ID of the project workspace.
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Originating sheet or section name (e.g., 'Tasks' for Excel).
    /// </summary>
    public string SheetName { get; set; } = "Default";

    /// <summary>
    /// 1-indexed row number within the source file.
    /// </summary>
    public int RowIndex { get; set; }

    /// <summary>
    /// Parsed title of the task.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Parsed task description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Parsed priority (Low, Medium, High, Urgent).
    /// </summary>
    public string Priority { get; set; } = "Medium";

    /// <summary>
    /// Parsed status (Open, InProgress, Closed).
    /// </summary>
    public string Status { get; set; } = "Open";

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
    public string ValidationStatus { get; set; } = "Valid";

    /// <summary>
    /// Specific cell or row error description if row failed validation.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
