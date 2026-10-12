using FluentValidation;

namespace BlazorFluent.Core.Domain.Imports;

/// <summary>
/// Tier 2 Entity Invariant Validator for StagedTaskEntity.
/// Enforces fail-closed database constraints prior to SaveChangesAsync.
/// </summary>
public class StagedTaskEntityValidator : AbstractValidator<StagedTaskEntity>
{
    public StagedTaskEntityValidator()
    {
        RuleFor(x => x.ImportFileId)
            .NotEmpty().WithMessage("Staged task must reference a parent ImportFileId.");

        RuleFor(x => x.ImportId)
            .NotEmpty().WithMessage("Staged task must reference an ImportId (Batch ID).");

        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage("Staged task must have a valid ProjectId.");

        RuleFor(x => x.RowIndex)
            .GreaterThan(0).WithMessage("Row index must be positive.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Staged task title is required.")
            .MaximumLength(200).WithMessage("Staged task title cannot exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(10000).WithMessage("Staged task description cannot exceed 10,000 characters.");

        RuleFor(x => x.Priority)
            .NotEmpty().WithMessage("Priority is required.")
            .MaximumLength(50).WithMessage("Priority string cannot exceed 50 characters.");

        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status is required.")
            .MaximumLength(50).WithMessage("Status string cannot exceed 50 characters.");

        RuleFor(x => x.AssigneeEmails)
            .MaximumLength(2000).WithMessage("Assignee emails cannot exceed 2000 characters.");

        RuleFor(x => x.Labels)
            .MaximumLength(500).WithMessage("Labels cannot exceed 500 characters.");
    }
}
