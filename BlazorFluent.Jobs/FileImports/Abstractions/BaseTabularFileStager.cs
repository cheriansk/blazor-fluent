using System.ComponentModel;
using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using BlazorFluent.Core.Abstractions.Imports;
using BlazorFluent.Core.Common;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Imports;
using BlazorFluent.Persistence.Context;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Jobs.FileImports.Abstractions;

/// <summary>
/// Reusable abstract base class for file stagers validating Excel (.xlsx) and CSV files.
/// Automatically executes:
/// a. Sheet name existence verification (for Excel workbooks).
/// b. Header column existence verification against [ImportColumn] attributes.
/// c. Required field validation (non-null/non-empty) for [ImportColumn(IsRequired = true)].
/// d. Automatic row-to-model hydration and fail-closed staging orchestration.
/// </summary>
/// <typeparam name="TModel">Strongly-typed template model decorated with [ImportSheet] and [ImportColumn].</typeparam>
public abstract class BaseTabularFileStager<TModel> : IFileStager
    where TModel : class, new()
{
    protected readonly AppDbContext DbContext;
    protected readonly ILogger Logger;

    private static readonly XNamespace SpreadsheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace RelationshipsNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelationshipsNs = "http://schemas.openxmlformats.org/package/2006/relationships";

    protected BaseTabularFileStager(AppDbContext dbContext, ILogger logger)
    {
        DbContext = dbContext;
        Logger = logger;
    }

    public abstract bool CanHandle(ImportFileType fileType);

    /// <summary>
    /// Optional hook for derived classes to execute entity-specific domain validation
    /// on hydrated rows AFTER basic sheet, header, and required field validation has passed.
    /// </summary>
    protected virtual Task<IReadOnlyList<FileValidationError>> ValidateSpecificAsync(
        ImportFileEntity file,
        IReadOnlyList<TModel> rows,
        CancellationToken ct)
    {
        return Task.FromResult<IReadOnlyList<FileValidationError>>(Array.Empty<FileValidationError>());
    }

    /// <summary>
    /// Implemented by derived stagers to insert validated, typed models into their respective staging tables.
    /// </summary>
    protected abstract Task<Result<int>> StageRowsAsync(
        ImportFileEntity file,
        IReadOnlyList<TModel> rows,
        CancellationToken ct);

    public virtual async Task<Result<int>> ValidateAndStageAsync(
        ImportFileEntity file,
        Stream decryptedStream,
        CancellationToken ct = default)
    {
        Logger.LogInformation("Starting tabular validation and staging for FileId={FileId} (Type={FileType})", file.Id, file.FileType);

        if (decryptedStream.CanSeek)
        {
            decryptedStream.Position = 0;
        }

        // 1. Mark Validating
        file.Status = ImportStatus.Validating;
        file.StepStage = "Validate";
        await DbContext.SaveChangesAsync(ct);

        // 2. Execute Abstract Base Validation & Hydration (Checks a: sheet, b: headers, c: required cells)
        var validationResult = await ValidateAndHydrateAsync(decryptedStream, file.FileType, ct);

        if (!validationResult.IsValid)
        {
            Logger.LogWarning("Base validation failed for FileId={FileId} with {ErrorCount} errors.", file.Id, validationResult.Errors.Count);

            file.Status = ImportStatus.ValidationFailed;
            file.TotalRowsCount = validationResult.ValidRows.Count;
            file.ValidRowsCount = 0;
            file.ErrorRowsCount = validationResult.Errors.Count;
            file.ValidationErrorsJson = JsonSerializer.Serialize(validationResult.Errors);
            file.ErrorMessage = $"Validation failed with {validationResult.Errors.Count} error(s). Strict fail-closed policy aborted staging.";
            file.ProcessedAtUtc = DateTime.UtcNow;
            await DbContext.SaveChangesAsync(ct);

            return Result<int>.Failure(file.ErrorMessage);
        }

        // 3. Execute Entity-Specific Domain Validation Hook
        var specificErrors = await ValidateSpecificAsync(file, validationResult.ValidRows, ct);
        if (specificErrors.Count > 0)
        {
            Logger.LogWarning("Specific domain validation failed for FileId={FileId} with {ErrorCount} errors.", file.Id, specificErrors.Count);

            file.Status = ImportStatus.ValidationFailed;
            file.TotalRowsCount = validationResult.ValidRows.Count;
            file.ValidRowsCount = 0;
            file.ErrorRowsCount = specificErrors.Count;
            file.ValidationErrorsJson = JsonSerializer.Serialize(specificErrors);
            file.ErrorMessage = $"Domain validation failed with {specificErrors.Count} error(s). Strict fail-closed policy aborted staging.";
            file.ProcessedAtUtc = DateTime.UtcNow;
            await DbContext.SaveChangesAsync(ct);

            return Result<int>.Failure(file.ErrorMessage);
        }

        // 4. Mark Staging & Invoke Concrete Staging Insert
        file.Status = ImportStatus.Staging;
        file.StepStage = "Stage";
        await DbContext.SaveChangesAsync(ct);

        try
        {
            var stageResult = await StageRowsAsync(file, validationResult.ValidRows, ct);

            if (!stageResult.IsSuccess)
            {
                file.Status = ImportStatus.StagingFailed;
                file.ErrorMessage = stageResult.Error;
                file.ProcessedAtUtc = DateTime.UtcNow;
                await DbContext.SaveChangesAsync(ct);
                return stageResult;
            }

            file.Status = ImportStatus.StagingSuccess;
            file.TotalRowsCount = validationResult.ValidRows.Count;
            file.ValidRowsCount = validationResult.ValidRows.Count;
            file.ErrorRowsCount = 0;
            file.ValidationErrorsJson = null;
            file.ErrorMessage = null;
            await DbContext.SaveChangesAsync(ct);

            Logger.LogInformation("Successfully staged {Count} records for FileId={FileId}", validationResult.ValidRows.Count, file.Id);
            return Result<int>.Success(validationResult.ValidRows.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Staging insert exception for FileId={FileId}", file.Id);

            file.Status = ImportStatus.StagingFailed;
            file.ErrorMessage = $"Staging insert failed: {ex.Message}";
            file.ProcessedAtUtc = DateTime.UtcNow;
            await DbContext.SaveChangesAsync(ct);

            return Result<int>.Failure(file.ErrorMessage);
        }
    }

    /// <summary>
    /// Executes abstract validation for sheet names, column headers, and required cell values.
    /// </summary>
    public async Task<TemplateValidationResult<TModel>> ValidateAndHydrateAsync(
        Stream stream,
        ImportFileType fileType,
        CancellationToken ct = default)
    {
        var modelType = typeof(TModel);
        var sheetAttr = modelType.GetCustomAttribute<ImportSheetAttribute>();
        var expectedSheetName = sheetAttr?.SheetName ?? "Main";

        var columnProperties = GetMappedProperties(modelType);
        var errors = new List<FileValidationError>();

        try
        {
            if (IsExcel(fileType))
            {
                return await ValidateAndHydrateExcelAsync(stream, expectedSheetName, columnProperties, errors, ct);
            }
            else
            {
                return await ValidateAndHydrateCsvAsync(stream, expectedSheetName, columnProperties, errors, ct);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Payload parsing error during tabular validation");
            errors.Add(new FileValidationError(expectedSheetName, 0, "File", $"File stream is corrupt or unreadable: {ex.Message}"));
            return TemplateValidationResult<TModel>.Failed(errors);
        }
    }

    #region Excel (.xlsx) Validation Engine

    private async Task<TemplateValidationResult<TModel>> ValidateAndHydrateExcelAsync(
        Stream stream,
        string expectedSheetName,
        List<PropertyColumnMapping> columnProperties,
        List<FileValidationError> errors,
        CancellationToken ct)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);

        // a. Check if expected sheet name exists in workbook.xml
        var workbookEntry = archive.GetEntry("xl/workbook.xml");
        if (workbookEntry == null)
        {
            errors.Add(new FileValidationError(expectedSheetName, 0, "Workbook", "Excel package is missing 'xl/workbook.xml'."));
            return TemplateValidationResult<TModel>.Failed(errors);
        }

        using var wbStream = workbookEntry.Open();
        var wbDoc = await XDocument.LoadAsync(wbStream, LoadOptions.None, ct);

        var sheetElements = wbDoc.Descendants(SpreadsheetNs + "sheet").ToList();
        var sheetElement = sheetElements.FirstOrDefault(s =>
            string.Equals(s.Attribute("name")?.Value, expectedSheetName, StringComparison.OrdinalIgnoreCase));

        if (sheetElement == null)
        {
            // Requirement a: Capture error and do remaining validation
            var actualSheets = string.Join(", ", sheetElements.Select(s => $"'{s.Attribute("name")?.Value}'"));
            errors.Add(new FileValidationError(
                expectedSheetName,
                0,
                "Sheet",
                $"Required worksheet '{expectedSheetName}' was not found in workbook. Available sheets: [{actualSheets}]."));

            // If the required sheet does not exist at all, we cannot validate columns for it, so fail-closed here
            return TemplateValidationResult<TModel>.Failed(errors);
        }

        // Resolve worksheet XML path via relationships
        var rId = sheetElement.Attribute(RelationshipsNs + "id")?.Value;
        var worksheetPath = "xl/worksheets/sheet1.xml";

        var relsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");
        if (relsEntry != null && !string.IsNullOrEmpty(rId))
        {
            using var relsStream = relsEntry.Open();
            var relsDoc = await XDocument.LoadAsync(relsStream, LoadOptions.None, ct);
            var targetRel = relsDoc.Descendants(PackageRelationshipsNs + "Relationship")
                .FirstOrDefault(r => r.Attribute("Id")?.Value == rId);

            var target = targetRel?.Attribute("Target")?.Value;
            if (!string.IsNullOrEmpty(target))
            {
                worksheetPath = target.StartsWith('/') ? target.TrimStart('/') : $"xl/{target}";
            }
        }

        var worksheetEntry = archive.GetEntry(worksheetPath);
        if (worksheetEntry == null)
        {
            errors.Add(new FileValidationError(expectedSheetName, 0, "Worksheet", $"Worksheet part '{worksheetPath}' was not found in package."));
            return TemplateValidationResult<TModel>.Failed(errors);
        }

        // Load Shared Strings table (if present)
        var sharedStrings = new List<string>();
        var ssEntry = archive.GetEntry("xl/sharedStrings.xml");
        if (ssEntry != null)
        {
            using var ssStream = ssEntry.Open();
            var ssDoc = await XDocument.LoadAsync(ssStream, LoadOptions.None, ct);
            foreach (var si in ssDoc.Descendants(SpreadsheetNs + "si"))
            {
                sharedStrings.Add(si.Value);
            }
        }

        using var wsStream = worksheetEntry.Open();
        var wsDoc = await XDocument.LoadAsync(wsStream, LoadOptions.None, ct);

        var rowElements = wsDoc.Descendants(SpreadsheetNs + "row").ToList();
        if (rowElements.Count == 0)
        {
            errors.Add(new FileValidationError(expectedSheetName, 0, "Worksheet", $"Worksheet '{expectedSheetName}' contains no data rows."));
            return TemplateValidationResult<TModel>.Failed(errors);
        }

        // b. Validate Column Headers (Row 1)
        var headerRow = rowElements[0];
        var fileHeaders = ExtractRowCellValues(headerRow, sharedStrings);

        var headerLookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < fileHeaders.Count; i++)
        {
            var header = fileHeaders[i]?.Trim();
            if (!string.IsNullOrEmpty(header) && !headerLookup.ContainsKey(header))
            {
                headerLookup[header] = i;
            }
        }

        foreach (var col in columnProperties)
        {
            if (!headerLookup.ContainsKey(col.HeaderName))
            {
                // Requirement b: check if all fields in template exist as columns
                errors.Add(new FileValidationError(
                    expectedSheetName,
                    1,
                    col.HeaderName,
                    $"Required column header '{col.HeaderName}' was not found in worksheet '{expectedSheetName}'."));
            }
        }

        // If headers are missing, we cannot reliably inspect column values for missing columns, but we continue row validation
        var hydratedRows = new List<TModel>();

        // c. Check fields marked as required have non-null, non-empty values across rows 2..N
        for (int r = 1; r < rowElements.Count; r++)
        {
            var rowElem = rowElements[r];
            var rowIndex = r + 1; // 1-based index matching Excel row number
            var rowValues = ExtractRowCellValues(rowElem, sharedStrings);

            var modelInstance = new TModel();
            bool rowHasError = false;

            // Stamp RowIndex if property exists
            var rowProp = typeof(TModel).GetProperty("RowIndex", BindingFlags.Public | BindingFlags.Instance);
            if (rowProp != null && rowProp.PropertyType == typeof(int) && rowProp.CanWrite)
            {
                rowProp.SetValue(modelInstance, rowIndex);
            }

            foreach (var col in columnProperties)
            {
                string? cellValue = null;
                if (headerLookup.TryGetValue(col.HeaderName, out var colIdx) && colIdx < rowValues.Count)
                {
                    cellValue = rowValues[colIdx]?.Trim();
                }

                // Requirement c: check fields marked required have non-null and non-empty value
                if (col.IsRequired && string.IsNullOrWhiteSpace(cellValue))
                {
                    errors.Add(new FileValidationError(
                        expectedSheetName,
                        rowIndex,
                        col.HeaderName,
                        col.ErrorMessage ?? $"Field '{col.HeaderName}' is marked as required and cannot be empty.",
                        cellValue));

                    rowHasError = true;
                }
                else if (!string.IsNullOrWhiteSpace(cellValue))
                {
                    try
                    {
                        var converted = ConvertValue(cellValue, col.Property.PropertyType);
                        col.Property.SetValue(modelInstance, converted);
                    }
                    catch (Exception ex)
                    {
                        errors.Add(new FileValidationError(
                            expectedSheetName,
                            rowIndex,
                            col.HeaderName,
                            $"Failed to parse value '{cellValue}' for property '{col.Property.Name}': {ex.Message}",
                            cellValue));

                        rowHasError = true;
                    }
                }
            }

            if (!rowHasError)
            {
                hydratedRows.Add(modelInstance);
            }
        }

        return errors.Count == 0
            ? TemplateValidationResult<TModel>.Success(hydratedRows)
            : TemplateValidationResult<TModel>.Failed(errors);
    }

    private static List<string> ExtractRowCellValues(XElement rowElem, List<string> sharedStrings)
    {
        var cells = rowElem.Elements(SpreadsheetNs + "c").ToList();
        var values = new List<string>();

        foreach (var c in cells)
        {
            var cellType = c.Attribute("t")?.Value;
            var rawVal = c.Element(SpreadsheetNs + "v")?.Value ?? string.Empty;

            if (cellType == "s" && int.TryParse(rawVal, out var strIdx) && strIdx >= 0 && strIdx < sharedStrings.Count)
            {
                values.Add(sharedStrings[strIdx]);
            }
            else
            {
                values.Add(rawVal);
            }
        }

        return values;
    }

    #endregion

    #region CSV / Delimited Validation Engine

    private async Task<TemplateValidationResult<TModel>> ValidateAndHydrateCsvAsync(
        Stream stream,
        string sheetName,
        List<PropertyColumnMapping> columnProperties,
        List<FileValidationError> errors,
        CancellationToken ct)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

        // b. Validate Column Headers (Row 1)
        var headerLine = await reader.ReadLineAsync(ct);
        if (string.IsNullOrWhiteSpace(headerLine))
        {
            errors.Add(new FileValidationError(sheetName, 1, "Header", "CSV file is empty or missing a header row."));
            return TemplateValidationResult<TModel>.Failed(errors);
        }

        char delimiter = headerLine.Contains('\t') ? '\t' : (headerLine.Contains(';') ? ';' : ',');
        var headers = headerLine.Split(delimiter)
            .Select(h => h.Trim().Trim('"'))
            .ToList();

        var headerLookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Count; i++)
        {
            var h = headers[i];
            if (!string.IsNullOrWhiteSpace(h) && !headerLookup.ContainsKey(h))
            {
                headerLookup[h] = i;
            }
        }

        foreach (var col in columnProperties)
        {
            if (!headerLookup.ContainsKey(col.HeaderName))
            {
                errors.Add(new FileValidationError(
                    sheetName,
                    1,
                    col.HeaderName,
                    $"Required column header '{col.HeaderName}' was not found in CSV header row."));
            }
        }

        var hydratedRows = new List<TModel>();
        var rowIndex = 2; // Data rows start at 2

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(line)) continue;

            var cols = line.Split(delimiter)
                .Select(c => c.Trim().Trim('"'))
                .ToArray();

            var modelInstance = new TModel();
            bool rowHasError = false;

            // Stamp RowIndex if property exists
            var rowProp = typeof(TModel).GetProperty("RowIndex", BindingFlags.Public | BindingFlags.Instance);
            if (rowProp != null && rowProp.PropertyType == typeof(int) && rowProp.CanWrite)
            {
                rowProp.SetValue(modelInstance, rowIndex);
            }

            foreach (var col in columnProperties)
            {
                string? cellValue = null;
                if (headerLookup.TryGetValue(col.HeaderName, out var colIdx) && colIdx < cols.Length)
                {
                    cellValue = cols[colIdx]?.Trim();
                }

                // Requirement c: check fields marked required have non-null and non-empty value
                if (col.IsRequired && string.IsNullOrWhiteSpace(cellValue))
                {
                    errors.Add(new FileValidationError(
                        sheetName,
                        rowIndex,
                        col.HeaderName,
                        col.ErrorMessage ?? $"Field '{col.HeaderName}' is marked as required and cannot be empty.",
                        cellValue));

                    rowHasError = true;
                }
                else if (!string.IsNullOrWhiteSpace(cellValue))
                {
                    try
                    {
                        var converted = ConvertValue(cellValue, col.Property.PropertyType);
                        col.Property.SetValue(modelInstance, converted);
                    }
                    catch (Exception ex)
                    {
                        errors.Add(new FileValidationError(
                            sheetName,
                            rowIndex,
                            col.HeaderName,
                            $"Failed to parse value '{cellValue}' for property '{col.Property.Name}': {ex.Message}",
                            cellValue));

                        rowHasError = true;
                    }
                }
            }

            if (!rowHasError)
            {
                hydratedRows.Add(modelInstance);
            }

            rowIndex++;
        }

        return errors.Count == 0
            ? TemplateValidationResult<TModel>.Success(hydratedRows)
            : TemplateValidationResult<TModel>.Failed(errors);
    }

    #endregion

    #region Reflection & Value Converters

    private static bool IsExcel(ImportFileType type) =>
        type is ImportFileType.TaskExcel;

    private static List<PropertyColumnMapping> GetMappedProperties(Type type)
    {
        var list = new List<PropertyColumnMapping>();
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var attr = prop.GetCustomAttribute<ImportColumnAttribute>();
            if (attr != null)
            {
                list.Add(new PropertyColumnMapping(prop, attr.HeaderName, attr.IsRequired, attr.ErrorMessage));
            }
        }
        return list;
    }

    private static object? ConvertValue(string value, Type targetType)
    {
        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlying == typeof(string)) return value;
        if (underlying == typeof(int)) return int.Parse(value, CultureInfo.InvariantCulture);
        if (underlying == typeof(long)) return long.Parse(value, CultureInfo.InvariantCulture);
        if (underlying == typeof(decimal)) return decimal.Parse(value, CultureInfo.InvariantCulture);
        if (underlying == typeof(double)) return double.Parse(value, CultureInfo.InvariantCulture);
        if (underlying == typeof(bool)) return bool.Parse(value);
        if (underlying == typeof(DateTime)) return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
        if (underlying == typeof(Guid)) return Guid.Parse(value);
        if (underlying.IsEnum) return Enum.Parse(underlying, value, ignoreCase: true);

        var converter = TypeDescriptor.GetConverter(underlying);
        if (converter.CanConvertFrom(typeof(string)))
        {
            return converter.ConvertFromInvariantString(value);
        }

        throw new NotSupportedException($"Type '{underlying.Name}' is not supported for automatic tabular conversion.");
    }

    protected record PropertyColumnMapping(
        PropertyInfo Property,
        string HeaderName,
        bool IsRequired,
        string? ErrorMessage);

    #endregion
}
