using BlazorFluent.Core.Abstractions.Imports;

namespace BlazorFluent.Jobs.FileImports.Tasks;

/// <summary>
/// Template model representing the expected schema of the 'Tasks' worksheet or CSV file.
/// Decorated with [ImportSheet] and [ImportColumn] for abstract schema and required validation.
/// </summary>
[ImportSheet("Tasks")]
public class TaskImportModel
{
    public int RowIndex { get; set; }

    [ImportColumn("Title", IsRequired = true, ErrorMessage = "Task Title is mandatory and cannot be empty.")]
    public string Title { get; set; } = string.Empty;

    [ImportColumn("Description", IsRequired = false)]
    public string? Description { get; set; }

    [ImportColumn("Priority", IsRequired = false)]
    public string? Priority { get; set; }

    [ImportColumn("Status", IsRequired = false)]
    public string? Status { get; set; }

    [ImportColumn("DueDate", IsRequired = false)]
    public DateTime? DueDate { get; set; }

    [ImportColumn("AssigneeEmails", IsRequired = false)]
    public string? AssigneeEmails { get; set; }

    [ImportColumn("Labels", IsRequired = false)]
    public string? Labels { get; set; }
}
