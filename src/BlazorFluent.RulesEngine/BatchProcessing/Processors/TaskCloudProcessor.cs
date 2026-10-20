using System.Globalization;
using BlazorFluent.Shared.Contracts.Engine.Transforms;
using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Shared.Models;

namespace BlazorFluent.RulesEngine.BatchProcessing.Processors;

/// <summary>
/// Strictly typed C# cloud processor for TaskCsv and TaskExcel payloads.
/// Enforces all-or-nothing batch atomicity: if any row fails domain rules, zero rows are returned for commit.
/// </summary>
public sealed class TaskCloudProcessor : BaseCloudProcessor
{
    private static readonly string[] SupportedTypes =
    [
        nameof(ImportFileTypeEnum.TaskCsv),
        nameof(ImportFileTypeEnum.TaskExcel)
    ];

    public override IReadOnlyCollection<string> SupportedFileTypes => SupportedTypes;

    public override ProcessBatchRespDto ProcessChunk(
        Guid importId,
        Guid importFileId,
        int chunkIndex,
        string fileType,
        CanonicalStagedChunkPayload payload,
        IReadOnlyDictionary<string, string>? contextParameters)
    {
        var reverseMap = BuildReverseHeaderMap(payload.HeaderMap);
        var errors = new List<FileValidationError>();
        var processedRows = new List<ProcessedDomainRowDto>(payload.Rows.Count);
        var sheetName = string.IsNullOrWhiteSpace(payload.SheetName) ? "Tasks" : payload.SheetName;

        foreach (var row in payload.Rows)
        {
            var title = GetCellValue(row, reverseMap, "Title")?.Trim();
            var description = GetCellValue(row, reverseMap, "Description")?.Trim();
            var statusRaw = GetCellValue(row, reverseMap, "Status")?.Trim();
            var priorityRaw = GetCellValue(row, reverseMap, "Priority")?.Trim();
            var dueDateRaw = GetCellValue(row, reverseMap, "DueDate")?.Trim();
            var storyPointsRaw = GetCellValue(row, reverseMap, "StoryPoints")?.Trim();

            if (string.IsNullOrWhiteSpace(title))
            {
                errors.Add(new FileValidationError(
                    SheetName: sheetName,
                    RowIndex: row.Idx,
                    ColumnName: "Title",
                    ErrorMessage: "Task Title is required by cloud domain rules.",
                    AttemptedValue: null));
                continue;
            }

            if (title.Length > 200)
            {
                errors.Add(new FileValidationError(
                    SheetName: sheetName,
                    RowIndex: row.Idx,
                    ColumnName: "Title",
                    ErrorMessage: "Task Title cannot exceed 200 characters.",
                    AttemptedValue: title));
                continue;
            }

            var normalizedStatus = NormalizeStatus(statusRaw, row.Idx, sheetName, errors);
            var normalizedPriority = NormalizePriority(priorityRaw, row.Idx, sheetName, errors);

            string? normalizedDueDateUtc = null;
            if (!string.IsNullOrWhiteSpace(dueDateRaw))
            {
                if (DateTime.TryParse(dueDateRaw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsedDate))
                {
                    normalizedDueDateUtc = parsedDate.ToString("O", CultureInfo.InvariantCulture);
                }
                else
                {
                    errors.Add(new FileValidationError(
                        SheetName: sheetName,
                        RowIndex: row.Idx,
                        ColumnName: "DueDate",
                        ErrorMessage: $"DueDate '{dueDateRaw}' is not a valid ISO-8601 date.",
                        AttemptedValue: dueDateRaw));
                }
            }

            string? normalizedStoryPoints = null;
            if (!string.IsNullOrWhiteSpace(storyPointsRaw))
            {
                if (int.TryParse(storyPointsRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var points) && points is >= 0 and <= 100)
                {
                    normalizedStoryPoints = points.ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    errors.Add(new FileValidationError(
                        SheetName: sheetName,
                        RowIndex: row.Idx,
                        ColumnName: "StoryPoints",
                        ErrorMessage: $"StoryPoints '{storyPointsRaw}' must be an integer between 0 and 100.",
                        AttemptedValue: storyPointsRaw));
                }
            }

            var domainProps = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Title"] = title,
                ["Description"] = string.IsNullOrWhiteSpace(description) ? null : description,
                ["Status"] = normalizedStatus,
                ["Priority"] = normalizedPriority,
                ["DueDateUtc"] = normalizedDueDateUtc,
                ["StoryPoints"] = normalizedStoryPoints
            };

            processedRows.Add(new ProcessedDomainRowDto(
                SourceRowId: row.Id,
                SourceRowIndex: row.Idx,
                TargetEntityName: "TaskEntity",
                UpsertKeyValue: null,
                IsEncryptedAtRest: false,
                EncryptedPayloadBytes: null,
                DomainProperties: domainProps));
        }

        // Strict All-or-Nothing Atomicity: if any row failed, reject the entire chunk with 0 rows committed.
        if (errors.Count > 0)
        {
            return new ProcessBatchRespDto(
                ImportFileId: importFileId,
                ChunkIndex: chunkIndex,
                IsSuccess: false,
                IsEncryptedAtRest: false,
                ProcessedRows: [],
                Errors: errors,
                ErrorMessage: $"Batch chunk validation failed with {errors.Count} error(s).");
        }

        return new ProcessBatchRespDto(
            ImportFileId: importFileId,
            ChunkIndex: chunkIndex,
            IsSuccess: true,
            IsEncryptedAtRest: false,
            ProcessedRows: processedRows,
            Errors: [],
            ErrorMessage: null);
    }

    private static string NormalizeStatus(string? statusRaw, int rowNumber, string sheetName, List<FileValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(statusRaw))
        {
            return nameof(BlazorFluent.Shared.DataListTypes.TaskStatusEnum.Open);
        }

        if (Enum.TryParse<BlazorFluent.Shared.DataListTypes.TaskStatusEnum>(statusRaw, ignoreCase: true, out var parsed))
        {
            return parsed.ToString();
        }

        switch (statusRaw.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant())
        {
            case "TODO":
            case "NEW":
            case "OPEN":
                return nameof(BlazorFluent.Shared.DataListTypes.TaskStatusEnum.Open);
            case "INPROGRESS":
            case "ACTIVE":
                return nameof(BlazorFluent.Shared.DataListTypes.TaskStatusEnum.InProgress);
            case "DONE":
            case "COMPLETED":
            case "CLOSED":
                return nameof(BlazorFluent.Shared.DataListTypes.TaskStatusEnum.Closed);
            case "PAUSED":
            case "ONHOLD":
                return nameof(BlazorFluent.Shared.DataListTypes.TaskStatusEnum.Paused);
            case "CANCELLED":
            case "CANCELED":
                return nameof(BlazorFluent.Shared.DataListTypes.TaskStatusEnum.Cancelled);
            default:
                errors.Add(new FileValidationError(
                    SheetName: sheetName,
                    RowIndex: rowNumber,
                    ColumnName: "Status",
                    ErrorMessage: $"Status '{statusRaw}' is not a recognized task status.",
                    AttemptedValue: statusRaw));
                return nameof(BlazorFluent.Shared.DataListTypes.TaskStatusEnum.Open);
        }
    }

    private static string NormalizePriority(string? priorityRaw, int rowNumber, string sheetName, List<FileValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(priorityRaw))
        {
            return nameof(TaskPriorityEnum.Medium);
        }

        if (Enum.TryParse<TaskPriorityEnum>(priorityRaw, ignoreCase: true, out var parsed))
        {
            return parsed.ToString();
        }

        if (string.Equals(priorityRaw, "Critical", StringComparison.OrdinalIgnoreCase))
        {
            return nameof(TaskPriorityEnum.Urgent);
        }

        errors.Add(new FileValidationError(
            SheetName: sheetName,
            RowIndex: rowNumber,
            ColumnName: "Priority",
            ErrorMessage: $"Priority '{priorityRaw}' is not a valid TaskPriority (Low, Medium, High, Urgent).",
            AttemptedValue: priorityRaw));
        return nameof(TaskPriorityEnum.Medium);
    }
}
