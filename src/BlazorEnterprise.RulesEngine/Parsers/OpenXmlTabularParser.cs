using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using BlazorEnterprise.Shared.Common;
using BlazorEnterprise.Shared.Models;

namespace BlazorEnterprise.RulesEngine.Parsers;

/// <summary>
/// Proprietary in-memory OpenXML Excel spreadsheet parser.
/// Reads worksheets, extracts shared string references, and validates columns against schema rules.
/// </summary>
public static class OpenXmlTabularParser
{
    public static TemplateValidationResult<IReadOnlyDictionary<string, string>> Parse(
        byte[] excelBytes,
        string targetSheetName,
        IReadOnlyList<string> requiredHeaders)
    {
        var errors = new List<FileValidationError>();
        var validRows = new List<IReadOnlyDictionary<string, string>>();

        if (excelBytes == null || excelBytes.Length == 0)
        {
            errors.Add(new FileValidationError(targetSheetName, 0, "Payload", "Excel file bytes are empty."));
            return TemplateValidationResult<IReadOnlyDictionary<string, string>>.Failed(errors);
        }

        try
        {
            using var stream = new MemoryStream(excelBytes);
            using var document = SpreadsheetDocument.Open(stream, false);

            var workbookPart = document.WorkbookPart;
            if (workbookPart == null)
            {
                errors.Add(new FileValidationError(targetSheetName, 0, "Workbook", "Workbook contains no data."));
                return TemplateValidationResult<IReadOnlyDictionary<string, string>>.Failed(errors);
            }

            var sheet = workbookPart.Workbook.Sheets?
                .Elements<Sheet>()
                .FirstOrDefault(s => string.Equals(s.Name?.Value, targetSheetName, StringComparison.OrdinalIgnoreCase));

            if (sheet?.Id?.Value == null)
            {
                errors.Add(new FileValidationError(targetSheetName, 0, "Sheet", $"Required sheet '{targetSheetName}' not found in workbook."));
                return TemplateValidationResult<IReadOnlyDictionary<string, string>>.Failed(errors);
            }

            var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id.Value);
            var sheetData = worksheetPart.Worksheet.Elements<SheetData>().FirstOrDefault();
            if (sheetData == null)
            {
                errors.Add(new FileValidationError(targetSheetName, 0, "SheetData", $"Sheet '{targetSheetName}' contains no rows."));
                return TemplateValidationResult<IReadOnlyDictionary<string, string>>.Failed(errors);
            }

            var stringTable = workbookPart.SharedStringTablePart?.SharedStringTable;
            var rows = sheetData.Elements<Row>().ToList();

            if (rows.Count == 0)
            {
                errors.Add(new FileValidationError(targetSheetName, 0, "Rows", "No rows found in sheet."));
                return TemplateValidationResult<IReadOnlyDictionary<string, string>>.Failed(errors);
            }

            // Header mapping (Row 1)
            var headerRow = rows[0];
            var columnHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var cell in headerRow.Elements<Cell>())
            {
                var colRef = GetColumnReference(cell.CellReference?.Value);
                var text = GetCellValue(cell, stringTable);
                if (!string.IsNullOrWhiteSpace(colRef) && !string.IsNullOrWhiteSpace(text))
                {
                    columnHeaders[colRef] = text.Trim();
                }
            }

            // Verify required headers
            foreach (var req in requiredHeaders)
            {
                if (!columnHeaders.Values.Any(h => string.Equals(h, req, StringComparison.OrdinalIgnoreCase)))
                {
                    errors.Add(new FileValidationError(targetSheetName, 1, req, $"Required header column '{req}' is missing from sheet '{targetSheetName}'."));
                }
            }

            if (errors.Count > 0)
            {
                return TemplateValidationResult<IReadOnlyDictionary<string, string>>.Failed(errors);
            }

            // Parse data rows (Row 2 onwards)
            for (var i = 1; i < rows.Count; i++)
            {
                var row = rows[i];
                var rowNumber = (int)(row.RowIndex?.Value ?? (uint)(i + 1));
                var rowData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var cell in row.Elements<Cell>())
                {
                    var colRef = GetColumnReference(cell.CellReference?.Value);
                    if (!string.IsNullOrWhiteSpace(colRef) && columnHeaders.TryGetValue(colRef, out var headerName))
                    {
                        var cellValue = GetCellValue(cell, stringTable);
                        rowData[headerName] = cellValue.Trim();
                    }
                }

                // Check required fields
                var isRowValid = true;
                foreach (var req in requiredHeaders)
                {
                    if (!rowData.TryGetValue(req, out var val) || string.IsNullOrWhiteSpace(val))
                    {
                        errors.Add(new FileValidationError(targetSheetName, rowNumber, req, $"Cell '{req}' cannot be empty on row {rowNumber}."));
                        isRowValid = false;
                    }
                }

                if (isRowValid)
                {
                    validRows.Add(rowData);
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add(new FileValidationError(targetSheetName, 0, "Parser", $"Excel parsing failure: {ex.Message}"));
        }

        return errors.Count == 0
            ? TemplateValidationResult<IReadOnlyDictionary<string, string>>.Success(validRows)
            : TemplateValidationResult<IReadOnlyDictionary<string, string>>.Failed(errors);
    }

    private static string GetColumnReference(string? cellReference)
    {
        if (string.IsNullOrWhiteSpace(cellReference)) return string.Empty;
        return new string(cellReference.TakeWhile(char.IsLetter).ToArray());
    }

    private static string GetCellValue(Cell cell, SharedStringTable? stringTable)
    {
        if (cell.CellValue == null) return string.Empty;
        var value = cell.CellValue.Text;

        if (cell.DataType != null && cell.DataType.Value == CellValues.SharedString && stringTable != null)
        {
            if (int.TryParse(value, out var id))
            {
                var item = stringTable.Elements<SharedStringItem>().ElementAtOrDefault(id);
                return item?.InnerText ?? string.Empty;
            }
        }

        return value;
    }
}
