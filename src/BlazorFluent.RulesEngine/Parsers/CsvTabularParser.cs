using System.Text;
using BlazorFluent.Shared.Common;
using BlazorFluent.Shared.Models;

namespace BlazorFluent.RulesEngine.Parsers;

/// <summary>
/// High-performance in-memory CSV parser supporting RFC 4180 compliance,
/// quote escaping, delimiter autodetection, and row-level validation.
/// </summary>
public static class CsvTabularParser
{
    public static TemplateValidationResult<IReadOnlyDictionary<string, string>> Parse(
        byte[] csvBytes,
        IReadOnlyList<string> requiredHeaders)
    {
        var errors = new List<FileValidationError>();
        var validRows = new List<IReadOnlyDictionary<string, string>>();

        if (csvBytes == null || csvBytes.Length == 0)
        {
            errors.Add(new FileValidationError("CSV", 0, "Payload", "CSV payload is empty."));
            return TemplateValidationResult<IReadOnlyDictionary<string, string>>.Failed(errors);
        }

        using var stream = new MemoryStream(csvBytes);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        var line = reader.ReadLine();
        if (line == null)
        {
            errors.Add(new FileValidationError("CSV", 0, "Header", "CSV has no lines or header row."));
            return TemplateValidationResult<IReadOnlyDictionary<string, string>>.Failed(errors);
        }

        var delimiter = DetectDelimiter(line);
        var headers = ParseLine(line, delimiter);

        // Header presence check
        for (var i = 0; i < requiredHeaders.Count; i++)
        {
            var req = requiredHeaders[i];
            if (!headers.Any(h => string.Equals(h.Trim(), req.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add(new FileValidationError("CSV", 1, req, $"Required header '{req}' was not found in CSV header row."));
            }
        }

        if (errors.Count > 0)
        {
            return TemplateValidationResult<IReadOnlyDictionary<string, string>>.Failed(errors);
        }

        var rowIndex = 1;
        while ((line = reader.ReadLine()) != null)
        {
            rowIndex++;
            if (string.IsNullOrWhiteSpace(line)) continue;

            var values = ParseLine(line, delimiter);
            var rowDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (var colIdx = 0; colIdx < headers.Count; colIdx++)
            {
                var header = headers[colIdx].Trim();
                var val = colIdx < values.Count ? values[colIdx].Trim() : string.Empty;
                rowDict[header] = val;
            }

            // Validate required columns
            var rowValid = true;
            foreach (var req in requiredHeaders)
            {
                if (rowDict.TryGetValue(req, out var val) && string.IsNullOrWhiteSpace(val))
                {
                    errors.Add(new FileValidationError("CSV", rowIndex, req, $"Column '{req}' cannot be empty on row {rowIndex}."));
                    rowValid = false;
                }
            }

            if (rowValid)
            {
                validRows.Add(rowDict);
            }
        }

        return errors.Count == 0
            ? TemplateValidationResult<IReadOnlyDictionary<string, string>>.Success(validRows)
            : TemplateValidationResult<IReadOnlyDictionary<string, string>>.Failed(errors);
    }

    private static char DetectDelimiter(string headerLine)
    {
        var commaCount = headerLine.Count(c => c == ',');
        var tabCount = headerLine.Count(c => c == '\t');
        var semicolonCount = headerLine.Count(c => c == ';');

        if (tabCount > commaCount && tabCount > semicolonCount) return '\t';
        if (semicolonCount > commaCount && semicolonCount > tabCount) return ';';
        return ',';
    }

    private static List<string> ParseLine(string line, char delimiter)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];

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
            else if (c == delimiter && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields;
    }
}
