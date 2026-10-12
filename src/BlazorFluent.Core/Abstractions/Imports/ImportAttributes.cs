namespace BlazorFluent.Core.Abstractions.Imports;

/// <summary>
/// Specifies the workbook worksheet name that a template model class corresponds to in Excel files.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class ImportSheetAttribute : Attribute
{
    public string SheetName { get; }

    public ImportSheetAttribute(string sheetName)
    {
        if (string.IsNullOrWhiteSpace(sheetName))
            throw new ArgumentException("Sheet name cannot be null or whitespace.", nameof(sheetName));

        SheetName = sheetName.Trim();
    }
}

/// <summary>
/// Specifies the column header name, required status, and validation message for a property in a tabular import payload.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public class ImportColumnAttribute : Attribute
{
    /// <summary>The expected column header in Row 1 of the Excel or CSV file.</summary>
    public string HeaderName { get; }

    /// <summary>Whether this cell must contain a non-null, non-whitespace value on every row.</summary>
    public bool IsRequired { get; set; }

    /// <summary>Optional custom error message when the required check fails.</summary>
    public string? ErrorMessage { get; set; }

    public ImportColumnAttribute(string headerName, bool isRequired = false, string? errorMessage = null)
    {
        if (string.IsNullOrWhiteSpace(headerName))
            throw new ArgumentException("Header name cannot be null or whitespace.", nameof(headerName));

        HeaderName = headerName.Trim();
        IsRequired = isRequired;
        ErrorMessage = errorMessage;
    }
}
