using BlazorFluent.Core.Abstractions.Imports;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Imports;
using BlazorFluent.Core.Dtos.Response;
using BlazorFluent.Jobs.FileImports.Abstractions;
using BlazorFluent.Persistence.Context;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Jobs.FileImports.Tasks;

/// <summary>
/// Step 1 & 2 Ingestion: Parses, strictly validates (fail-closed), and stages project tasks.
/// Tasks are strictly imported as CSV.
/// Inherits from <see cref="BaseTabularFileStager{TModel}"/> which automatically:
/// 1. Executes base validation (headers matching [ImportColumn], non-null required cells).
/// 2. Hydrates rows into <see cref="TaskImportModel"/>.
/// 3. Calls <see cref="ValidateSpecificAsync"/> for domain-level task rules.
/// 4. Calls <see cref="StageRowsAsync"/> to insert valid rows into staging.StagedTasks.
/// </summary>
public class TaskValidateAndStage : BaseTabularFileStager<TaskImportModel>
{
    private static readonly HashSet<string> ValidPriorities = new(StringComparer.OrdinalIgnoreCase)
    {
        "Low", "Medium", "High", "Critical"
    };

    private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Open", "InProgress", "InReview", "Blocked", "Closed"
    };

    public TaskValidateAndStage(AppDbContext dbContext, ILogger<TaskValidateAndStage> logger)
        : base(dbContext, logger)
    {
    }

    /// <summary>
    /// Tasks are strictly imported as CSV files and never as any other format.
    /// </summary>
    public override bool CanHandle(ImportFileType fileType) => fileType == ImportFileType.TaskCsv;

    /// <summary>
    /// Executes entity-specific domain validation on hydrated rows AFTER basic CSV validation has passed.
    /// </summary>
    protected override Task<IReadOnlyList<FileValidationError>> ValidateSpecificAsync(
        ImportFileEntity file,
        IReadOnlyList<TaskImportModel> rows,
        CancellationToken ct)
    {
        var domainErrors = new List<FileValidationError>();

        for (int i = 0; i < rows.Count; i++)
        {
            var task = rows[i];
            var rowIndex = task.RowIndex > 0 ? task.RowIndex : i + 2;

            if (task.Title.Length > 200)
            {
                domainErrors.Add(new FileValidationError(
                    "Tasks",
                    rowIndex,
                    nameof(task.Title),
                    "Task title cannot exceed 200 characters.",
                    task.Title[..Math.Min(task.Title.Length, 50)] + "..."));
            }

            if (!string.IsNullOrEmpty(task.Description) && task.Description.Length > 10000)
            {
                domainErrors.Add(new FileValidationError(
                    "Tasks",
                    rowIndex,
                    nameof(task.Description),
                    "Task description cannot exceed 10,000 characters."));
            }

            if (!string.IsNullOrWhiteSpace(task.Priority) && !ValidPriorities.Contains(task.Priority.Trim()))
            {
                domainErrors.Add(new FileValidationError(
                    "Tasks",
                    rowIndex,
                    nameof(task.Priority),
                    $"Invalid Priority '{task.Priority}'. Allowed: Low, Medium, High, Critical.",
                    task.Priority));
            }

            if (!string.IsNullOrWhiteSpace(task.Status) && !ValidStatuses.Contains(task.Status.Trim()))
            {
                domainErrors.Add(new FileValidationError(
                    "Tasks",
                    rowIndex,
                    nameof(task.Status),
                    $"Invalid Status '{task.Status}'. Allowed: Open, InProgress, InReview, Blocked, Closed.",
                    task.Status));
            }

            if (task.DueDate.HasValue && task.DueDate.Value.Year < 2000)
            {
                domainErrors.Add(new FileValidationError(
                    "Tasks",
                    rowIndex,
                    nameof(task.DueDate),
                    "DueDate year cannot be earlier than 2000.",
                    task.DueDate.Value.ToString("yyyy-MM-dd")));
            }
        }

        return Task.FromResult<IReadOnlyList<FileValidationError>>(domainErrors);
    }

    /// <summary>
    /// Executes staging insert after both base and specific validation have confirmed 0 errors.
    /// </summary>
    protected override async Task<Result<int>> StageRowsAsync(
        ImportFileEntity file,
        IReadOnlyList<TaskImportModel> rows,
        CancellationToken ct)
    {
        if (rows.Count == 0)
        {
            return Result<int>.Failure("The CSV file contains no data rows to stage.");
        }

        var stagedRecords = new List<StagedTaskEntity>(rows.Count);
        foreach (var task in rows)
        {
            stagedRecords.Add(new StagedTaskEntity
            {
                Id = Guid.CreateVersion7(),
                ImportId = file.ImportId,
                ImportFileId = file.Id,
                ProjectId = file.ProjectId,
                TenantId = file.TenantId,
                SheetName = "Tasks",
                RowIndex = task.RowIndex,
                Title = task.Title.Trim(),
                Description = task.Description?.Trim() ?? string.Empty,
                Priority = string.IsNullOrWhiteSpace(task.Priority) ? "Medium" : task.Priority.Trim(),
                Status = string.IsNullOrWhiteSpace(task.Status) ? "Open" : task.Status.Trim(),
                DueDate = task.DueDate,
                AssigneeEmails = task.AssigneeEmails?.Trim(),
                Labels = task.Labels?.Trim(),
                ValidationStatus = "Valid",
                ErrorMessage = null
            });
        }

        await DbContext.StagedTasks.AddRangeAsync(stagedRecords, ct);
        return Result<int>.Success(stagedRecords.Count);
    }
}
