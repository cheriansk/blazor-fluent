namespace BlazorEnterprise.Shared.Models;

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

/// <summary>
/// Encapsulates the complete outcome of sheet, header, and cell-level validation,
/// including hydrated model instances if all validation passes.
/// </summary>
public record TemplateValidationResult<TModel>(
    bool IsValid,
    IReadOnlyList<TModel> ValidRows,
    IReadOnlyList<FileValidationError> Errors)
{
    public static TemplateValidationResult<TModel> Success(IReadOnlyList<TModel> rows) =>
        new(true, rows, Array.Empty<FileValidationError>());

    public static TemplateValidationResult<TModel> Failed(IReadOnlyList<FileValidationError> errors) =>
        new(false, Array.Empty<TModel>(), errors);
}
