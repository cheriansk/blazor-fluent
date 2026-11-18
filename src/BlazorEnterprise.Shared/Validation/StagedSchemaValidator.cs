using System.Globalization;
using System.Text.Json;
using BlazorEnterprise.Shared.Models;

namespace BlazorEnterprise.Shared.Validation;

/// <summary>
/// Tier 1 Generic Schema Validator shared by both <c>BlazorEnterprise.Jobs</c> and <c>BlazorEnterprise.Api</c>/<c>RulesEngine</c>.
/// Validates staged sheets, delimited text lines, and extracted document rows against template schema rules
/// and returns unified <see cref="FileValidationError"/> items.
/// </summary>
public static class StagedSchemaValidator
{
    public sealed record SchemaColumnRule(
        string Name,
        bool IsRequired = false,
        string? DataType = null);

    public sealed record SchemaSheetRule(
        string SheetName,
        bool IsRequired = false,
        IReadOnlyList<SchemaColumnRule>? Columns = null);

    public sealed record SchemaDefinition(
        IReadOnlyList<SchemaSheetRule> Sheets);

    public sealed record DelimitedParseResult(
        IReadOnlyList<string> Headers,
        IReadOnlyList<Dictionary<string, string?>> Rows,
        string DetectedDelimiter);

    /// <summary>
    /// Validates a tabular sheet (Headers + Rows) against a <see cref="SchemaSheetRule"/>.
    /// </summary>
    public static FileValidationResult ValidateTabularSheet(
        string sheetName,
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows,
        SchemaSheetRule? sheetRule)
    {
        var errors = new List<FileValidationError>();

        if (sheetRule is null)
        {
            return new FileValidationResult(true, errors);
        }

        var headerSet = new HashSet<string>(
            headers.Where(h => !string.IsNullOrWhiteSpace(h)).Select(h => h.Trim()),
            StringComparer.OrdinalIgnoreCase);

        if (sheetRule.Columns is not null)
        {
            foreach (var col in sheetRule.Columns)
            {
                if (col.IsRequired && !headerSet.Contains(col.Name))
                {
                    errors.Add(new FileValidationError(
                        sheetName,
                        RowIndex: 0,
                        ColumnName: col.Name,
                        ErrorMessage: $"Required column '{col.Name}' is missing from '{sheetName}'."));
                }
            }

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var rowNumber = i + 1;

                foreach (var col in sheetRule.Columns)
                {
                    row.TryGetValue(col.Name, out var rawValue);
                    var trimmed = rawValue?.Trim();

                    if (col.IsRequired && string.IsNullOrWhiteSpace(trimmed))
                    {
                        errors.Add(new FileValidationError(
                            sheetName,
                            RowIndex: rowNumber,
                            ColumnName: col.Name,
                            ErrorMessage: $"Required value for '{col.Name}' is missing or empty.",
                            AttemptedValue: rawValue));
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(trimmed) && !string.IsNullOrWhiteSpace(col.DataType))
                    {
                        if (!IsValidDataType(trimmed, col.DataType))
                        {
                            errors.Add(new FileValidationError(
                                sheetName,
                                RowIndex: rowNumber,
                                ColumnName: col.Name,
                                ErrorMessage: $"Value '{trimmed}' is not a valid {col.DataType} for '{col.Name}'.",
                                AttemptedValue: rawValue));
                        }
                    }
                }
            }
        }

        return new FileValidationResult(errors.Count == 0, errors);
    }

    /// <summary>
    /// Validates that all required sheets exist in the provided sheet names.
    /// </summary>
    public static IReadOnlyList<FileValidationError> ValidateRequiredSheets(
        IEnumerable<string> presentSheetNames,
        SchemaDefinition? schema)
    {
        var errors = new List<FileValidationError>();
        if (schema is null)
        {
            return errors;
        }

        var present = new HashSet<string>(
            presentSheetNames.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()),
            StringComparer.OrdinalIgnoreCase);

        foreach (var sheet in schema.Sheets.Where(s => s.IsRequired))
        {
            if (!present.Contains(sheet.SheetName))
            {
                errors.Add(new FileValidationError(
                    sheet.SheetName,
                    RowIndex: 0,
                    ColumnName: string.Empty,
                    ErrorMessage: $"Required sheet or section '{sheet.SheetName}' is missing."));
            }
        }

        return errors;
    }

    /// <summary>
    /// Splits raw delimited text lines (e.g. from <c>{"Lines": [...]}</c> in <c>staging.StagedImports</c>)
    /// using <c>||</c>, <c>;</c>, <c>\t</c>, or <c>,</c> into headers and row dictionaries.
    /// </summary>
    public static DelimitedParseResult ParseDelimitedLines(
        IReadOnlyList<string> lines,
        string? explicitDelimiter = null)
    {
        var nonEmptyLines = lines
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        if (nonEmptyLines.Count == 0)
        {
            return new DelimitedParseResult([], [], explicitDelimiter ?? ",");
        }

        var delimiter = !string.IsNullOrEmpty(explicitDelimiter)
            ? explicitDelimiter
            : DetectDelimiter(nonEmptyLines[0]);

        var headers = SplitDelimitedLine(nonEmptyLines[0], delimiter)
            .Select(h => h.Trim())
            .ToList();

        var rows = new List<Dictionary<string, string?>>(Math.Max(0, nonEmptyLines.Count - 1));
        for (var i = 1; i < nonEmptyLines.Count; i++)
        {
            var cells = SplitDelimitedLine(nonEmptyLines[i], delimiter);
            var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            for (var c = 0; c < headers.Count; c++)
            {
                var header = headers[c];
                if (string.IsNullOrWhiteSpace(header))
                {
                    continue;
                }

                dict[header] = c < cells.Count ? cells[c].Trim() : null;
            }

            rows.Add(dict);
        }

        return new DelimitedParseResult(headers, rows, delimiter);
    }

    public static string DetectDelimiter(string headerLine)
    {
        if (headerLine.Contains("||", StringComparison.Ordinal))
        {
            return "||";
        }

        if (headerLine.Contains('\t'))
        {
            return "\t";
        }

        if (headerLine.Contains(';'))
        {
            return ";";
        }

        return ",";
    }

    public static List<string> SplitDelimitedLine(string line, string delimiter)
    {
        if (delimiter.Length > 1)
        {
            return line.Split(delimiter, StringSplitOptions.None).ToList();
        }

        var sep = delimiter[0];
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
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
            else if (ch == sep && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        result.Add(current.ToString());
        return result;
    }

    /// <summary>
    /// Parses a <see cref="SchemaDefinition"/> from a JSON string if present; returns <c>null</c> if empty or invalid.
    /// </summary>
    public static SchemaDefinition? TryParseSchemaJson(string? schemaJson)
    {
        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SchemaDefinition>(
                schemaJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsValidDataType(string value, string dataType)
    {
        return dataType.Trim().ToLowerInvariant() switch
        {
            "int" or "integer" or "int32" => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
            "long" or "int64" => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
            "decimal" or "number" or "numeric" or "double" => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _),
            "bool" or "boolean" => bool.TryParse(value, out _) || value is "0" or "1" or "yes" or "no" or "Yes" or "No",
            "date" or "datetime" or "utc" => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out _),
            "guid" or "uuid" => Guid.TryParse(value, out _),
            _ => true
        };
    }
}
