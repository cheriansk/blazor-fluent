using FluentValidation;

namespace BlazorFluent.Core.Domain.Imports;

/// <summary>
/// Tier 2 Entity Invariant Validator for ImportFileEntity.
/// Validates database-level constraints before persistence in SaveChangesAsync.
/// Strictly self-contained: inspects only ImportFileEntity properties without external lookups.
/// </summary>
public class ImportFileEntityValidator : AbstractValidator<ImportFileEntity>
{
    public ImportFileEntityValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage("Import file must be associated with a valid ProjectId.");

        RuleFor(x => x.ImportId)
            .NotEmpty().WithMessage("Import file must be associated with a valid ImportId (Batch ID).");

        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("Import file name is required.")
            .MaximumLength(260).WithMessage("Import file name cannot exceed 260 characters.");

        RuleFor(x => x.OriginalFileName)
            .NotEmpty().WithMessage("Original file name is required.")
            .MaximumLength(260).WithMessage("Original file name cannot exceed 260 characters.");

        RuleFor(x => x.BlobUri)
            .NotEmpty().WithMessage("Blob URI storage path is required.")
            .MaximumLength(1024).WithMessage("Blob URI cannot exceed 1024 characters.");

        RuleFor(x => x.FileSizeBytes)
            .GreaterThan(0).WithMessage("File size must be greater than 0 bytes.");

        RuleFor(x => x.Sha256Checksum)
            .NotEmpty().WithMessage("SHA-256 checksum is mandatory for tamper-detection.")
            .Length(64).WithMessage("SHA-256 checksum must be exactly 64 hex characters.");

        RuleFor(x => x.TotalRowsCount)
            .GreaterThanOrEqualTo(0).WithMessage("Total rows count cannot be negative.");

        RuleFor(x => x.ValidRowsCount)
            .GreaterThanOrEqualTo(0).WithMessage("Valid rows count cannot be negative.");

        RuleFor(x => x.ErrorRowsCount)
            .GreaterThanOrEqualTo(0).WithMessage("Error rows count cannot be negative.");
    }
}
