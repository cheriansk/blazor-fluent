namespace BlazorFluent.Core.Abstractions.Imports;

/// <summary>
/// Represents a discrete cell-level, column-level, or row-level validation failure in an imported payload.
/// </summary>
public record FileValidationError(
    string SheetName,
    int RowIndex,
    string ColumnName,
    string ErrorMessage,
    string? AttemptedValue = null);

/// <summary>
/// Result of a schema-level and data-level validation execution.
/// </summary>
public record FileValidationResult(
    bool IsValid,
    IReadOnlyList<FileValidationError> Errors)
{
    public static FileValidationResult Success() => new(true, Array.Empty<FileValidationError>());
    public static FileValidationResult Failed(IReadOnlyList<FileValidationError> errors) => new(false, errors);
}
