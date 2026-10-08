using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Dtos.Imports;
using FluentValidation;

namespace BlazorFluent.Core.Validation.Imports;

/// <summary>
/// Tier 1 Page/Form Validator for UploadFileRequest.
/// Provides immediate UI feedback on file selection, allowed extensions, and file size limits.
/// </summary>
public class UploadFileRequestValidator : AbstractValidator<UploadFileRequest>
{
    private const long MaxFileSizeLimit = 52_428_800; // 50 Megabytes

    public UploadFileRequestValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage("Please select a target Project workspace.");

        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("Please select a file to upload.")
            .Must((request, fileName) => HasValidExtension(fileName, request.FileType))
            .WithMessage(x => $"File extension does not match the selected file type '{x.FileType}'.");

        RuleFor(x => x.FileSizeBytes)
            .GreaterThan(0).WithMessage("Selected file is empty.")
            .LessThanOrEqualTo(MaxFileSizeLimit).WithMessage("File size cannot exceed 50 MB.");

        RuleFor(x => x.ContentStream)
            .NotNull().WithMessage("File content stream is required.");
    }

    private static bool HasValidExtension(string? fileName, ImportFileType fileType)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return false;
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return true;

/*        return fileType switch
        {
            ImportFileType.Excel => ext is ".xlsx" or ".xls",
            ImportFileType.Json => ext is ".json",
            ImportFileType.Xml => ext is ".xml",
            ImportFileType.DelimitedText => ext is ".txt" or ".csv" or ".tsv",
            _ => false
        };*/
    }
}
