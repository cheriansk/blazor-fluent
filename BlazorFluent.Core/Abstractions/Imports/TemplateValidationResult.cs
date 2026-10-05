namespace BlazorFluent.Core.Abstractions.Imports;

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
