using System.Globalization;
using System.Text.RegularExpressions;
using BlazorEnterprise.Shared.Contracts.Engine.Transforms;
using BlazorEnterprise.Shared.DataListTypes;
using BlazorEnterprise.Shared.Models;

namespace BlazorEnterprise.RulesEngine.BatchProcessing.Processors;

/// <summary>
/// Strictly typed C# cloud processor for ProgramCalendarExcel and ProjectCalendarExcel payloads.
/// Enforces domain rules, sequential Timeline -> Milestone parent linking, date bounds, and email format.
/// Enforces all-or-nothing batch atomicity: if any row fails, zero rows are returned for commit.
/// </summary>
public sealed class CalendarCloudProcessor : BaseCloudProcessor
{
    private static readonly string[] SupportedTypes =
    [
        nameof(ImportFileTypeEnum.ProgramCalendarExcel),
        nameof(ImportFileTypeEnum.ProjectCalendarExcel)
    ];

    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        TimeSpan.FromMilliseconds(100));

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
        var sheetName = string.IsNullOrWhiteSpace(payload.SheetName) ? "Calendar" : payload.SheetName;

        var isProgramCalendar = string.Equals(fileType, nameof(ImportFileTypeEnum.ProgramCalendarExcel), StringComparison.OrdinalIgnoreCase);
        var targetEntity = isProgramCalendar ? "ProgramCalendarEntity" : "ProjectCalendarEntity";

        Guid? currentTimelineSourceRowId = null;
        string? currentTimelineTitle = null;
        DateTime? currentTimelineStartUtc = null;
        DateTime? currentTimelineEndUtc = null;

        foreach (var row in payload.Rows)
        {
            var entryTypeRaw = GetCellValue(row, reverseMap, "EntryType")?.Trim();
            var title = GetCellValue(row, reverseMap, "Title")?.Trim();
            var startDateRaw = GetCellValue(row, reverseMap, "StartDate")?.Trim();
            var endDateRaw = GetCellValue(row, reverseMap, "EndDate")?.Trim();
            var statusRaw = GetCellValue(row, reverseMap, "Status")?.Trim();
            var ownerEmailsRaw = GetCellValue(row, reverseMap, "OwnerEmails")?.Trim();
            var comments = GetCellValue(row, reverseMap, "Comments")?.Trim();
            var programCode = GetCellValue(row, reverseMap, "ProgramCode")?.Trim();

            // 1. EntryType check
            CalendarEntryTypeEnum entryType;
            if (string.Equals(entryTypeRaw, "Timeline", StringComparison.OrdinalIgnoreCase))
            {
                entryType = CalendarEntryTypeEnum.Timeline;
            }
            else if (string.Equals(entryTypeRaw, "Milestone", StringComparison.OrdinalIgnoreCase))
            {
                entryType = CalendarEntryTypeEnum.Milestone;
            }
            else
            {
                errors.Add(new FileValidationError(
                    SheetName: sheetName,
                    RowIndex: row.Idx,
                    ColumnName: "EntryType",
                    ErrorMessage: $"Invalid EntryType '{entryTypeRaw}'. Allowed values: Timeline, Milestone.",
                    AttemptedValue: entryTypeRaw));
                continue;
            }

            // 2. Title validation
            if (string.IsNullOrWhiteSpace(title))
            {
                errors.Add(new FileValidationError(
                    SheetName: sheetName,
                    RowIndex: row.Idx,
                    ColumnName: "Title",
                    ErrorMessage: $"{entryType} Title is required.",
                    AttemptedValue: null));
                continue;
            }

            if (title.Length > 200)
            {
                errors.Add(new FileValidationError(
                    SheetName: sheetName,
                    RowIndex: row.Idx,
                    ColumnName: "Title",
                    ErrorMessage: $"{entryType} Title cannot exceed 200 characters.",
                    AttemptedValue: title));
                continue;
            }

            // 3. Date validation
            DateTime startDateUtc = default;
            if (string.IsNullOrWhiteSpace(startDateRaw) || !TryParseIsoOrExcelDate(startDateRaw, out startDateUtc))
            {
                errors.Add(new FileValidationError(
                    SheetName: sheetName,
                    RowIndex: row.Idx,
                    ColumnName: "StartDate",
                    ErrorMessage: $"StartDate '{startDateRaw}' is not a valid date.",
                    AttemptedValue: startDateRaw));
            }

            DateTime endDateUtc = default;
            if (string.IsNullOrWhiteSpace(endDateRaw) || !TryParseIsoOrExcelDate(endDateRaw, out endDateUtc))
            {
                errors.Add(new FileValidationError(
                    SheetName: sheetName,
                    RowIndex: row.Idx,
                    ColumnName: "EndDate",
                    ErrorMessage: $"EndDate '{endDateRaw}' is not a valid date.",
                    AttemptedValue: endDateRaw));
            }

            if (startDateUtc != default && endDateUtc != default && endDateUtc < startDateUtc)
            {
                errors.Add(new FileValidationError(
                    SheetName: sheetName,
                    RowIndex: row.Idx,
                    ColumnName: "EndDate",
                    ErrorMessage: $"EndDate ({endDateUtc:yyyy-MM-dd}) cannot be earlier than StartDate ({startDateUtc:yyyy-MM-dd}).",
                    AttemptedValue: endDateRaw));
            }

            // 4. Sequential Hierarchy Validation (Timeline vs Milestone)
            if (entryType == CalendarEntryTypeEnum.Timeline)
            {
                currentTimelineSourceRowId = row.Id;
                currentTimelineTitle = title;
                currentTimelineStartUtc = startDateUtc;
                currentTimelineEndUtc = endDateUtc;
            }
            else // Milestone
            {
                if (!currentTimelineSourceRowId.HasValue)
                {
                    errors.Add(new FileValidationError(
                        SheetName: sheetName,
                        RowIndex: row.Idx,
                        ColumnName: "EntryType",
                        ErrorMessage: $"Milestone '{title}' on row {row.Idx} must be preceded by a parent Timeline row in the sheet.",
                        AttemptedValue: entryTypeRaw));
                }
                else
                {
                    // Verify Milestone date bounds fall within parent Timeline bounds
                    if (startDateUtc != default && currentTimelineStartUtc.HasValue && startDateUtc < currentTimelineStartUtc.Value)
                    {
                        errors.Add(new FileValidationError(
                            SheetName: sheetName,
                            RowIndex: row.Idx,
                            ColumnName: "StartDate",
                            ErrorMessage: $"Milestone '{title}' StartDate ({startDateUtc:yyyy-MM-dd}) cannot be earlier than parent Timeline '{currentTimelineTitle}' StartDate ({currentTimelineStartUtc.Value:yyyy-MM-dd}).",
                            AttemptedValue: startDateRaw));
                    }

                    if (endDateUtc != default && currentTimelineEndUtc.HasValue && endDateUtc > currentTimelineEndUtc.Value)
                    {
                        errors.Add(new FileValidationError(
                            SheetName: sheetName,
                            RowIndex: row.Idx,
                            ColumnName: "EndDate",
                            ErrorMessage: $"Milestone '{title}' EndDate ({endDateUtc:yyyy-MM-dd}) cannot be later than parent Timeline '{currentTimelineTitle}' EndDate ({currentTimelineEndUtc.Value:yyyy-MM-dd}).",
                            AttemptedValue: endDateRaw));
                    }
                }

                // OwnerEmails is mandatory for Milestone rows
                if (string.IsNullOrWhiteSpace(ownerEmailsRaw))
                {
                    errors.Add(new FileValidationError(
                        SheetName: sheetName,
                        RowIndex: row.Idx,
                        ColumnName: "OwnerEmails",
                        ErrorMessage: $"OwnerEmails is required for Milestone '{title}'.",
                        AttemptedValue: null));
                }
                else
                {
                    var emails = ownerEmailsRaw.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    foreach (var email in emails)
                    {
                        if (!EmailRegex.IsMatch(email))
                        {
                            errors.Add(new FileValidationError(
                                SheetName: sheetName,
                                RowIndex: row.Idx,
                                ColumnName: "OwnerEmails",
                                ErrorMessage: $"Owner email '{email}' is not a valid email address.",
                                AttemptedValue: email));
                        }
                    }
                }
            }

            // 5. Status normalization
            var normalizedStatus = NormalizeStatus(statusRaw, row.Idx, sheetName, errors);

            var domainProps = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["EntryType"] = entryType.ToString(),
                ["Title"] = title,
                ["StartDateUtc"] = startDateUtc != default ? startDateUtc.ToString("O", CultureInfo.InvariantCulture) : null,
                ["EndDateUtc"] = endDateUtc != default ? endDateUtc.ToString("O", CultureInfo.InvariantCulture) : null,
                ["Status"] = normalizedStatus,
                ["OwnerEmails"] = ownerEmailsRaw,
                ["Comments"] = comments,
                ["ProgramCode"] = programCode,
                ["ParentTimelineSourceRowId"] = entryType == CalendarEntryTypeEnum.Milestone && currentTimelineSourceRowId.HasValue
                    ? currentTimelineSourceRowId.Value.ToString()
                    : null,
                ["ParentTimelineTitle"] = entryType == CalendarEntryTypeEnum.Milestone
                    ? currentTimelineTitle
                    : null
            };

            processedRows.Add(new ProcessedDomainRowDto(
                SourceRowId: row.Id,
                SourceRowIndex: row.Idx,
                TargetEntityName: targetEntity,
                UpsertKeyValue: title,
                IsEncryptedAtRest: false,
                EncryptedPayloadBytes: null,
                DomainProperties: domainProps));
        }

        // Strict All-or-Nothing Atomicity: if any row failed, reject the entire chunk with 0 rows committed.
        if (errors.Count > 0)
        {
            return new ProcessBatchRespDto(
                TenantId: payload.TenantId,
                ImportFileId: importFileId,
                ChunkIndex: chunkIndex,
                IsSuccess: false,
                IsEncryptedAtRest: false,
                ProcessedRows: [],
                Errors: errors,
                ErrorMessage: $"Calendar batch chunk validation failed with {errors.Count} error(s).");
        }

        return new ProcessBatchRespDto(
            TenantId: payload.TenantId,
            ImportFileId: importFileId,
            ChunkIndex: chunkIndex,
            IsSuccess: true,
            IsEncryptedAtRest: false,
            ProcessedRows: processedRows,
            Errors: [],
            ErrorMessage: null);
    }

    private static bool TryParseIsoOrExcelDate(string val, out DateTime parsed)
    {
        if (DateTime.TryParse(val, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out parsed))
        {
            return true;
        }

        if (double.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out var oaDate) && oaDate is > 1000 and < 100000)
        {
            try
            {
                parsed = DateTime.SpecifyKind(DateTime.FromOADate(oaDate), DateTimeKind.Utc);
                return true;
            }
            catch
            {
                // Ignore
            }
        }

        parsed = default;
        return false;
    }

    private static string NormalizeStatus(string? statusRaw, int rowNumber, string sheetName, List<FileValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(statusRaw))
        {
            return nameof(CalendarEntryStatusEnum.New);
        }

        if (statusRaw.TryToEnum<CalendarEntryStatusEnum>(out var parsed))
        {
            return parsed.ToString();
        }

        switch (statusRaw.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant())
        {
            case "NEW":
            case "TODO":
            case "OPEN":
                return nameof(CalendarEntryStatusEnum.New);
            case "SCHEDULED":
            case "PLANNED":
                return nameof(CalendarEntryStatusEnum.Scheduled);
            case "CONFIRMED":
                return nameof(CalendarEntryStatusEnum.Confirmed);
            case "INPROGRESS":
            case "ACTIVE":
                return nameof(CalendarEntryStatusEnum.InProgress);
            case "COMPLETED":
            case "DONE":
            case "CLOSED":
                return nameof(CalendarEntryStatusEnum.Completed);
            case "DELAYED":
            case "LATE":
                return nameof(CalendarEntryStatusEnum.Delayed);
            case "CANCELLED":
            case "CANCELED":
                return nameof(CalendarEntryStatusEnum.Cancelled);
            default:
                errors.Add(new FileValidationError(
                    SheetName: sheetName,
                    RowIndex: rowNumber,
                    ColumnName: "Status",
                    ErrorMessage: $"Status '{statusRaw}' is not a recognized calendar status.",
                    AttemptedValue: statusRaw));
                return nameof(CalendarEntryStatusEnum.New);
        }
    }
}
