using FluentValidation;

namespace BlazorFluent.Core.Domain.Imports;

/// <summary>
/// Tier 2 Entity Invariant Validator for StagedImportEntity ('staging.StagedImports').
/// Enforces fail-closed database constraints prior to SaveChangesAsync.
/// </summary>
public class StagedImportEntityValidator : AbstractValidator<StagedImportEntity>
{
    public StagedImportEntityValidator()
    {
        RuleFor(x => x.ImportFileId)
            .NotEmpty().WithMessage("Staged import chunk must reference a parent ImportFileId.");

        RuleFor(x => x.ImportId)
            .NotEmpty().WithMessage("Staged import chunk must reference an ImportId (Batch ID).");

        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage("Staged import chunk must have a valid ProjectId.");

        RuleFor(x => x.FileType)
            .NotEmpty().WithMessage("FileType is required.")
            .MaximumLength(100).WithMessage("FileType cannot exceed 100 characters.");

        RuleFor(x => x.OriginalFileName)
            .NotEmpty().WithMessage("OriginalFileName is required.")
            .MaximumLength(500).WithMessage("OriginalFileName cannot exceed 500 characters.");

        RuleFor(x => x.ChunkIndex)
            .GreaterThan(0).WithMessage("ChunkIndex must be positive.");

        RuleFor(x => x.TotalChunks)
            .GreaterThan(0).WithMessage("TotalChunks must be positive.");

        RuleFor(x => x.RowCount)
            .GreaterThan(0).WithMessage("RowCount must be positive.");

        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status is required.")
            .MaximumLength(50).WithMessage("Status cannot exceed 50 characters.");

        RuleFor(x => x.PayloadJson)
            .NotEmpty().WithMessage("PayloadJson cannot be empty.");
    }
}
