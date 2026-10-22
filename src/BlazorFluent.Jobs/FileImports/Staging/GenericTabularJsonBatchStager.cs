using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using BlazorFluent.Core.Domain.Imports;
using BlazorFluent.Core.Dtos.Response;
using BlazorFluent.Persistence.Context;
using BlazorFluent.Shared.Common;
using BlazorFluent.Shared.Contracts.Engine.Transforms;
using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Jobs.FileImports.Staging;

/// <summary>
/// Generic config-driven batch stager ('generic-json') that streams Excel (.xlsx), CSV, space-delimited text, or XML,
/// validates against FileTemplateConfig rules, and stages 1,000-row high-density canonical JSON chunks into 'staging.StagedImports'.
/// </summary>
public sealed class GenericTabularJsonBatchStager : ICanonicalBatchStager
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<GenericTabularJsonBatchStager> _logger;

    private static readonly XNamespace SpreadsheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace RelationshipsNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelationshipsNs = "http://schemas.openxmlformats.org/package/2006/relationships";

    public string StrategyName => "generic-json";

    public GenericTabularJsonBatchStager(
        AppDbContext dbContext,
        ILogger<GenericTabularJsonBatchStager> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Result<int>> ValidateAndStageCanonicalJsonAsync(
        ImportFileEntity file,
        Stream decryptedStream,
        FileTemplateConfig config,
        CancellationToken ct = default)
    {
        if (decryptedStream.CanSeek)
            decryptedStream.Position = 0;

        file.Status = ImportStatusEnum.Validating;
        file.StepStage = "Validate";
        await _dbContext.SaveChangesAsync(ct);

        var errors = new List<FileValidationError>();
        var maxErrors = config.MaxErrorThreshold > 0 ? config.MaxErrorThreshold : 500;
        var chunkSize = config.ChunkSize > 0 ? config.ChunkSize : 1000;

        var isExcel = IsExcelFormat(file, config);
        var xmlSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };

        ZipArchive? archive = null;
        List<ExcelSheetMetadata> discoveredExcelSheets = [];
        Dictionary<string, ExcelSheetMetadata> discoveredExcelSheetMap = new(StringComparer.OrdinalIgnoreCase);
        XDocument? relsDoc = null;
        List<string> sharedStrings = [];

        List<SheetProcessingPlan> sheetPlans = [];

        try
        {
            if (isExcel)
            {
                archive = new ZipArchive(decryptedStream, ZipArchiveMode.Read, leaveOpen: true);
                var (sheets, rels, strings) = LoadExcelWorkbookMetadata(archive, xmlSettings, errors);
                discoveredExcelSheets = sheets;
                discoveredExcelSheetMap = sheets.ToDictionary(s => s.Name, s => s, StringComparer.OrdinalIgnoreCase);
                relsDoc = rels;
                sharedStrings = strings;

                if (errors.Count == 0)
                {
                    if (config.Sheets is { Count: > 0 })
                    {
                        // 1. Fail-Closed on Unknown Sheets: Reject file if workbook contains unauthorized worksheets
                        var configuredSheetNames = new HashSet<string>(config.Sheets.Select(s => s.Name), StringComparer.OrdinalIgnoreCase);
                        foreach (var discovered in discoveredExcelSheets)
                        {
                            if (!configuredSheetNames.Contains(discovered.Name))
                            {
                                errors.Add(new FileValidationError(
                                    discovered.Name,
                                    0,
                                    "Sheet",
                                    $"Excel workbook contains unauthorized/unknown worksheet '{discovered.Name}'. Rejected under fail-closed multi-sheet policy. Allowed sheets: [{string.Join(", ", config.Sheets.Select(s => $"'{s.Name}'"))}]."));
                            }
                        }

                        // 2. Fail-Closed on Missing Required Sheets
                        foreach (var sheetCfg in config.Sheets)
                        {
                            if (sheetCfg.Required && !discoveredExcelSheetMap.ContainsKey(sheetCfg.Name))
                            {
                                var foundNames = string.Join(", ", discoveredExcelSheets.Select(s => $"'{s.Name}'"));
                                errors.Add(new FileValidationError(
                                    sheetCfg.Name,
                                    0,
                                    "Sheet",
                                    $"Required worksheet '{sheetCfg.Name}' was not found in the Excel workbook. Found: [{foundNames}]."));
                            }
                        }

                        // 3. Plan sheets in declaration order from config.Sheets
                        sheetPlans = config.Sheets
                            .Where(s => discoveredExcelSheetMap.ContainsKey(s.Name))
                            .Select(s => new SheetProcessingPlan(
                                s.Name,
                                s.Columns is { Count: > 0 } sc ? sc : config.Columns,
                                s.UniqueKeys is { Count: > 0 } sk ? sk : config.UniqueKeys))
                            .ToList();
                    }
                    else
                    {
                        // Fallback: Dynamic staging using workbook sheets with global template column rules
                        sheetPlans = discoveredExcelSheets
                            .Select(s => new SheetProcessingPlan(s.Name, config.Columns, config.UniqueKeys))
                            .ToList();
                    }
                }
            }
            else
            {
                var targetSheetName = config.Sheets.FirstOrDefault()?.Name ?? "Default";
                var effectiveColumns = config.Sheets.FirstOrDefault()?.Columns is { Count: > 0 } sheetCols
                    ? sheetCols
                    : config.Columns;
                var uniqueKeys = config.Sheets.FirstOrDefault()?.UniqueKeys is { Count: > 0 } sKeys
                    ? sKeys
                    : config.UniqueKeys;

                sheetPlans = [new SheetProcessingPlan(targetSheetName, effectiveColumns, uniqueKeys)];
            }

            var stagedChunksToPersist = new List<(string SheetName, int ChunkIndex, int TotalChunks, int RowCount, bool IsLastChunk, Dictionary<string, string> HeaderMap, StagedRowDto[] Rows)>();
            int totalRowsRead = 0;

            if (errors.Count == 0)
            {
                foreach (var plan in sheetPlans)
                {
                    ct.ThrowIfCancellationRequested();

                    var columnRules = plan.Columns.ToDictionary(c => c.Name, c => c, StringComparer.OrdinalIgnoreCase);
                    var seenUniqueCompositeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var headerMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    var columnToAlias = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                    int aliasCounter = 1;
                    foreach (var col in plan.Columns)
                    {
                        var alias = !string.IsNullOrWhiteSpace(col.Alias) ? col.Alias! : $"c{aliasCounter++}";
                        while (headerMap.ContainsKey(alias))
                        {
                            alias = $"c{aliasCounter++}";
                        }
                        headerMap[alias] = col.Name;
                        columnToAlias[col.Name] = alias;
                    }

                    var rawRows = isExcel
                        ? ReadExcelSheetRows(archive!, discoveredExcelSheetMap[plan.SheetName], relsDoc, sharedStrings, xmlSettings, plan.SheetName, errors)
                        : ReadDelimitedRows(decryptedStream, plan.SheetName, config.Format, errors);

                    var sheetStagedRows = new List<StagedRowDto>();
                    IReadOnlyList<string>? discoveredHeaders = null;

                    foreach (var (rowIndex, rowCells, headers) in rawRows)
                    {
                        ct.ThrowIfCancellationRequested();

                        if (discoveredHeaders == null)
                        {
                            discoveredHeaders = headers;

                            // Ensure all discovered headers have an alias in headerMap (zero-maintenance dynamic aliasing)
                            foreach (var header in discoveredHeaders)
                            {
                                if (!columnToAlias.ContainsKey(header))
                                {
                                    var alias = $"c{aliasCounter++}";
                                    while (headerMap.ContainsKey(alias))
                                    {
                                        alias = $"c{aliasCounter++}";
                                    }
                                    headerMap[alias] = header;
                                    columnToAlias[header] = alias;
                                }
                            }

                            // Check required headers exist
                            foreach (var reqCol in plan.Columns.Where(c => c.Required))
                            {
                                if (!discoveredHeaders.Any(h => string.Equals(h, reqCol.Name, StringComparison.OrdinalIgnoreCase)))
                                {
                                    errors.Add(new FileValidationError(
                                        plan.SheetName,
                                        1,
                                        reqCol.Name,
                                        $"Required header column '{reqCol.Name}' is missing from sheet '{plan.SheetName}'."));
                                }
                            }

                            if (errors.Count > 0)
                                break;
                        }

                        totalRowsRead++;
                        ValidateRow(rowIndex, plan.SheetName, rowCells, columnRules, plan.UniqueKeys, seenUniqueCompositeKeys, errors);

                        if (errors.Count >= maxErrors)
                        {
                            _logger.LogWarning("Fail-fast error threshold ({MaxErrors}) reached for FileId={FileId}. Halting validation.", maxErrors, file.Id);
                            break;
                        }

                        if (errors.Count == 0)
                        {
                            var compactData = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                            foreach (var kvp in rowCells)
                            {
                                var val = kvp.Value?.Trim();
                                if (config.OmitNullValues && string.IsNullOrEmpty(val))
                                    continue;

                                var aliasKey = columnToAlias.TryGetValue(kvp.Key, out var mappedAlias) ? mappedAlias : kvp.Key;
                                compactData[aliasKey] = val;
                            }

                            sheetStagedRows.Add(new StagedRowDto(
                                Id: Guid.CreateVersion7(),
                                Idx: rowIndex,
                                D: compactData));
                        }
                    }

                    if (errors.Count >= maxErrors)
                        break;

                    // Chunk rows per sheet (ChunkIndex 1..N scoped to this sheet)
                    if (sheetStagedRows.Count > 0)
                    {
                        var chunks = sheetStagedRows.Chunk(chunkSize).ToList();
                        int sheetTotalChunks = chunks.Count;

                        for (int i = 0; i < sheetTotalChunks; i++)
                        {
                            int chunkIndex = i + 1;
                            bool isLastChunk = chunkIndex == sheetTotalChunks;
                            stagedChunksToPersist.Add((plan.SheetName, chunkIndex, sheetTotalChunks, chunks[i].Length, isLastChunk, new Dictionary<string, string>(headerMap), chunks[i]));
                        }
                    }
                }
            }

            if (errors.Count == 0 && stagedChunksToPersist.Count == 0)
            {
                errors.Add(new FileValidationError("Workbook", 0, "Rows", "The imported file contains no data rows to stage across any worksheet."));
            }

            // All-or-Nothing Fail-Closed Policy: If any error exists, abort and stage 0 chunks
            if (errors.Count > 0)
            {
                file.Status = ImportStatusEnum.ValidationFailed;
                file.StepStage = "ValidationFailed";
                file.TotalRowsCount = totalRowsRead;
                file.ValidRowsCount = 0;
                file.ErrorRowsCount = errors.Count;
                file.ValidationErrorsJson = JsonSerializer.Serialize(errors, JsonDefaults.StandardOptions);
                file.ErrorMessage = $"Validation failed with {errors.Count} error(s). All-or-nothing policy aborted staging.";
                file.ProcessedAtUtc = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(ct);

                return Result<int>.Failure(file.ErrorMessage);
            }

            // Stage into 'staging.StagedImports' in chunks of ChunkSize (1,000 rows default)
            file.Status = ImportStatusEnum.Staging;
            file.StepStage = "Stage";
            await _dbContext.SaveChangesAsync(ct);

            // Remove any prior uncommitted staged chunks for idempotency
            var existingChunks = await _dbContext.StagedImports
                .Where(s => s.ImportFileId == file.Id)
                .ToListAsync(ct);
            if (existingChunks.Count > 0)
            {
                _dbContext.StagedImports.RemoveRange(existingChunks);
                await _dbContext.SaveChangesAsync(ct);
            }

            foreach (var chunk in stagedChunksToPersist)
            {
                var payload = new CanonicalStagedChunkPayload(
                    ImportId: file.ImportId,
                    ImportFileId: file.Id,
                    FileType: config.FileType,
                    SheetName: chunk.SheetName,
                    ChunkIndex: chunk.ChunkIndex,
                    TotalChunks: chunk.TotalChunks,
                    RowCount: chunk.RowCount,
                    IsLastChunk: chunk.IsLastChunk,
                    HeaderMap: chunk.HeaderMap,
                    Rows: chunk.Rows);

                var payloadJson = JsonSerializer.Serialize(payload, JsonDefaults.StandardOptions);

                var stagedEntity = new StagedImportEntity
                {
                    Id = Guid.CreateVersion7(),
                    TenantId = file.TenantId,
                    ProjectId = file.ProjectId,
                    ImportId = file.ImportId,
                    ImportFileId = file.Id,
                    FileType = config.FileType,
                    OriginalFileName = file.OriginalFileName,
                    SheetName = chunk.SheetName,
                    ChunkIndex = chunk.ChunkIndex,
                    TotalChunks = chunk.TotalChunks,
                    RowCount = chunk.RowCount,
                    IsProcessed = false,
                    Status = "Staged",
                    PayloadJson = payloadJson
                };

                _dbContext.StagedImports.Add(stagedEntity);
            }

            await _dbContext.SaveChangesAsync(ct);

            int totalStagedRows = stagedChunksToPersist.Sum(c => c.RowCount);
            file.Status = ImportStatusEnum.StagingSuccess;
            file.StepStage = "Staged";
            file.TotalRowsCount = totalStagedRows;
            file.ValidRowsCount = totalStagedRows;
            file.ErrorRowsCount = 0;
            file.ErrorMessage = null;
            await _dbContext.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Successfully staged {RowCount} rows across {ChunkCount} canonical JSON chunk(s) into staging.StagedImports for FileId={FileId}.",
                totalStagedRows, stagedChunksToPersist.Count, file.Id);

            return Result<int>.Success(totalStagedRows);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected failure during validation and staging for FileId={FileId}.", file.Id);
            file.Status = ImportStatusEnum.ValidationFailed;
            file.StepStage = "ValidationFailed";
            file.ErrorMessage = $"Staging failure: {ex.Message}";
            file.ProcessedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(ct);

            return Result<int>.Failure(file.ErrorMessage);
        }
        finally
        {
            archive?.Dispose();
        }
    }

    private static bool IsExcelFormat(ImportFileEntity file, FileTemplateConfig config)
    {
        if (string.Equals(config.Format.Type, "excel", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(config.Format.Type, "delimited", StringComparison.OrdinalIgnoreCase))
            return false;

        return file.FileType == ImportFileTypeEnum.TaskExcel ||
               file.OriginalFileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateRow(
        int rowIndex,
        string sheetName,
        IReadOnlyDictionary<string, string?> rowCells,
        IReadOnlyDictionary<string, ColumnTemplateConfig> columnRules,
        IReadOnlyList<string> uniqueKeys,
        HashSet<string> seenUniqueCompositeKeys,
        List<FileValidationError> errors)
    {
        foreach (var rule in columnRules.Values)
        {
            rowCells.TryGetValue(rule.Name, out var rawVal);
            var val = rawVal?.Trim();

            if (string.IsNullOrWhiteSpace(val))
            {
                if (rule.Required)
                {
                    errors.Add(new FileValidationError(
                        sheetName,
                        rowIndex,
                        rule.Name,
                        $"Required field '{rule.Name}' cannot be empty on row {rowIndex}."));
                }
                continue;
            }

            if (rule.MaxLength.HasValue && val.Length > rule.MaxLength.Value)
            {
                errors.Add(new FileValidationError(
                    sheetName,
                    rowIndex,
                    rule.Name,
                    $"Field '{rule.Name}' exceeds maximum length of {rule.MaxLength.Value} characters.",
                    val[..Math.Min(val.Length, 50)]));
            }

            if (rule.AllowedValues is { Count: > 0 } &&
                !rule.AllowedValues.Any(a => string.Equals(a, val, StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add(new FileValidationError(
                    sheetName,
                    rowIndex,
                    rule.Name,
                    $"Invalid value '{val}' for '{rule.Name}'. Allowed: {string.Join(", ", rule.AllowedValues)}.",
                    val));
            }

            if (!string.IsNullOrWhiteSpace(rule.RegexPattern) &&
                !Regex.IsMatch(val, rule.RegexPattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
            {
                errors.Add(new FileValidationError(
                    sheetName,
                    rowIndex,
                    rule.Name,
                    $"Field '{rule.Name}' does not match required pattern.",
                    val));
            }

            // DataType check
            switch (rule.DataType.ToLowerInvariant())
            {
                case "integer":
                case "int":
                    if (!long.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                        errors.Add(new FileValidationError(sheetName, rowIndex, rule.Name, $"Field '{rule.Name}' must be a valid integer.", val));
                    break;
                case "decimal":
                case "number":
                    if (!decimal.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                        errors.Add(new FileValidationError(sheetName, rowIndex, rule.Name, $"Field '{rule.Name}' must be a valid decimal number.", val));
                    break;
                case "date":
                case "datetime":
                    if (!TryParseExcelOrIsoDate(val, out var parsedDate))
                    {
                        errors.Add(new FileValidationError(sheetName, rowIndex, rule.Name, $"Field '{rule.Name}' must be a valid date.", val));
                    }
                    else if (parsedDate.Year < 2000)
                    {
                        errors.Add(new FileValidationError(sheetName, rowIndex, rule.Name, $"Field '{rule.Name}' year cannot be earlier than 2000.", val));
                    }
                    break;
                case "boolean":
                case "bool":
                    if (!bool.TryParse(val, out _) && val is not ("0" or "1" or "yes" or "no" or "Yes" or "No"))
                        errors.Add(new FileValidationError(sheetName, rowIndex, rule.Name, $"Field '{rule.Name}' must be a valid boolean.", val));
                    break;
                case "guid":
                    if (!Guid.TryParse(val, out _))
                        errors.Add(new FileValidationError(sheetName, rowIndex, rule.Name, $"Field '{rule.Name}' must be a valid GUID.", val));
                    break;
            }
        }

        // Composite Unique Keys check
        if (uniqueKeys.Count > 0)
        {
            var keyParts = uniqueKeys
                .Select(k => rowCells.TryGetValue(k, out var v) ? v?.Trim() ?? string.Empty : string.Empty)
                .ToArray();

            if (keyParts.Any(p => !string.IsNullOrEmpty(p)))
            {
                var compositeKey = string.Join("||", keyParts);
                if (!seenUniqueCompositeKeys.Add(compositeKey))
                {
                    var keyName = string.Join("+", uniqueKeys);
                    errors.Add(new FileValidationError(
                        sheetName,
                        rowIndex,
                        keyName,
                        $"Duplicate unique key ({keyName} = '{compositeKey}') detected on row {rowIndex}.",
                        compositeKey));
                }
            }
        }
    }

    private static bool TryParseExcelOrIsoDate(string val, out DateTime parsed)
    {
        if (DateTime.TryParse(val, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out parsed))
            return true;

        if (double.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out var oaDate) && oaDate is > 1000 and < 100000)
        {
            try
            {
                parsed = DateTime.SpecifyKind(DateTime.FromOADate(oaDate), DateTimeKind.Utc);
                return true;
            }
            catch
            {
                // Ignore invalid OA date
            }
        }

        parsed = default;
        return false;
    }

    private static IEnumerable<(int RowIndex, Dictionary<string, string?> Cells, IReadOnlyList<string> Headers)> ReadDelimitedRows(
        Stream stream,
        string sheetName,
        FileFormatConfig format,
        List<FileValidationError> errors)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 16384, leaveOpen: true);
        var headerLine = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(headerLine))
        {
            errors.Add(new FileValidationError(sheetName, 1, "Header", "The file is empty or missing the header row."));
            yield break;
        }

        var delimiter = string.IsNullOrEmpty(format.Delimiter) ? "," : format.Delimiter;
        var headers = SplitDelimitedLine(headerLine, delimiter, format.IgnoreMultipleSpaces)
            .Select(h => h.Trim())
            .Where(h => !string.IsNullOrEmpty(h))
            .ToList();

        int rowIndex = 1;
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            rowIndex++;
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var values = SplitDelimitedLine(line, delimiter, format.IgnoreMultipleSpaces);
            var rowDict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < headers.Count; i++)
            {
                rowDict[headers[i]] = i < values.Count ? values[i] : null;
            }

            yield return (rowIndex, rowDict, headers);
        }
    }

    private static List<string> SplitDelimitedLine(string line, string delimiter, bool ignoreMultipleSpaces)
    {
        if (delimiter == " " && ignoreMultipleSpaces)
        {
            return line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }

        var result = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;
        char delimChar = delimiter[0];

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == delimChar && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        result.Add(current.ToString());
        return result;
    }

    private static (List<ExcelSheetMetadata> Sheets, XDocument? RelsDoc, List<string> SharedStrings) LoadExcelWorkbookMetadata(
        ZipArchive archive,
        XmlReaderSettings xmlSettings,
        List<FileValidationError> errors)
    {
        var workbookEntry = archive.GetEntry("xl/workbook.xml");
        if (workbookEntry == null)
        {
            errors.Add(new FileValidationError("Workbook", 0, "Workbook", "Invalid .xlsx archive: missing xl/workbook.xml."));
            return ([], null, []);
        }

        XDocument workbookDoc;
        using (var wbStream = workbookEntry.Open())
        using (var wbReader = XmlReader.Create(wbStream, xmlSettings))
        {
            workbookDoc = XDocument.Load(wbReader);
        }

        var sheets = workbookDoc.Descendants(SpreadsheetNs + "sheet")
            .Select(s => new ExcelSheetMetadata(
                s.Attribute("name")?.Value ?? string.Empty,
                s.Attribute(RelationshipsNs + "id")?.Value ?? string.Empty))
            .ToList();

        XDocument? relsDoc = null;
        var relsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");
        if (relsEntry != null)
        {
            using var relsStream = relsEntry.Open();
            using var relsReader = XmlReader.Create(relsStream, xmlSettings);
            relsDoc = XDocument.Load(relsReader);
        }

        var sharedStrings = new List<string>();
        var sharedStringsEntry = archive.GetEntry("xl/sharedStrings.xml");
        if (sharedStringsEntry != null)
        {
            using var ssStream = sharedStringsEntry.Open();
            using var ssReader = XmlReader.Create(ssStream, xmlSettings);
            var ssDoc = XDocument.Load(ssReader);
            foreach (var si in ssDoc.Descendants(SpreadsheetNs + "si"))
            {
                var text = string.Concat(si.Descendants(SpreadsheetNs + "t").Select(t => t.Value));
                sharedStrings.Add(text);
            }
        }

        return (sheets, relsDoc, sharedStrings);
    }

    private static IEnumerable<(int RowIndex, Dictionary<string, string?> Cells, IReadOnlyList<string> Headers)> ReadExcelSheetRows(
        ZipArchive archive,
        ExcelSheetMetadata sheetMeta,
        XDocument? relsDoc,
        IReadOnlyList<string> sharedStrings,
        XmlReaderSettings xmlSettings,
        string targetSheetName,
        List<FileValidationError> errors)
    {
        string sheetPath = "xl/worksheets/sheet1.xml";
        if (relsDoc != null)
        {
            var relTarget = relsDoc.Descendants(PackageRelationshipsNs + "Relationship")
                .FirstOrDefault(r => r.Attribute("Id")?.Value == sheetMeta.RelId)?
                .Attribute("Target")?.Value;

            if (!string.IsNullOrWhiteSpace(relTarget))
            {
                sheetPath = relTarget.StartsWith('/') ? relTarget.TrimStart('/') : $"xl/{relTarget.TrimStart('/')}";
            }
        }

        var sheetEntry = archive.GetEntry(sheetPath);
        if (sheetEntry == null)
        {
            errors.Add(new FileValidationError(targetSheetName, 0, "Sheet", $"Worksheet XML part '{sheetPath}' not found in archive."));
            yield break;
        }

        using var wsStream = sheetEntry.Open();
        using var wsReader = XmlReader.Create(wsStream, xmlSettings);

        Dictionary<int, string>? headerColumnsByIndex = null;
        List<string>? headerList = null;
        int currentRowIndex = 0;

        while (wsReader.Read())
        {
            if (wsReader.NodeType == XmlNodeType.Element && wsReader.LocalName == "row")
            {
                currentRowIndex++;
                var rowAttr = wsReader.GetAttribute("r");
                int rowNum = int.TryParse(rowAttr, out var parsedRowNum) ? parsedRowNum : currentRowIndex;

                using var rowSubtree = wsReader.ReadSubtree();
                var rowElement = XElement.Load(rowSubtree);
                var cellsByColIdx = new Dictionary<int, string?>();

                int fallbackColIdx = 0;
                foreach (var c in rowElement.Elements(SpreadsheetNs + "c"))
                {
                    var cellRef = c.Attribute("r")?.Value;
                    int colIdx = !string.IsNullOrWhiteSpace(cellRef) ? ParseExcelColumnIndex(cellRef) : fallbackColIdx;
                    fallbackColIdx = colIdx + 1;

                    var cellType = c.Attribute("t")?.Value;
                    string? rawVal = c.Element(SpreadsheetNs + "v")?.Value;

                    if (cellType == "s" && int.TryParse(rawVal, out var ssIdx) && ssIdx >= 0 && ssIdx < sharedStrings.Count)
                    {
                        rawVal = sharedStrings[ssIdx];
                    }
                    else if (cellType == "inlineStr")
                    {
                        rawVal = string.Concat(c.Descendants(SpreadsheetNs + "t").Select(t => t.Value));
                    }

                    cellsByColIdx[colIdx] = rawVal;
                }

                if (headerColumnsByIndex == null)
                {
                    headerColumnsByIndex = new Dictionary<int, string>();
                    headerList = new List<string>();
                    foreach (var kvp in cellsByColIdx)
                    {
                        var h = kvp.Value?.Trim();
                        if (!string.IsNullOrEmpty(h))
                        {
                            headerColumnsByIndex[kvp.Key] = h;
                            headerList.Add(h);
                        }
                    }
                    continue;
                }

                // Skip completely empty rows
                if (cellsByColIdx.Values.All(string.IsNullOrWhiteSpace))
                    continue;

                var rowDict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in headerColumnsByIndex)
                {
                    rowDict[kvp.Value] = cellsByColIdx.TryGetValue(kvp.Key, out var val) ? val : null;
                }

                yield return (rowNum, rowDict, headerList!);
            }
        }
    }

    private static int ParseExcelColumnIndex(string cellReference)
    {
        int colIndex = 0;
        foreach (char ch in cellReference)
        {
            if (char.IsLetter(ch))
            {
                colIndex = colIndex * 26 + (char.ToUpperInvariant(ch) - 'A' + 1);
            }
            else
            {
                break;
            }
        }
        return colIndex - 1;
    }

    private sealed record SheetProcessingPlan(
        string SheetName,
        IReadOnlyList<ColumnTemplateConfig> Columns,
        IReadOnlyList<string> UniqueKeys);

    private sealed record ExcelSheetMetadata(string Name, string RelId);
}
