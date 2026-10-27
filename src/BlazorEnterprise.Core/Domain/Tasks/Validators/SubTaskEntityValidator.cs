using FluentValidation;

namespace BlazorEnterprise.Core.Domain.Tasks.Validators;

public class SubTaskEntityValidator : AbstractValidator<SubTaskEntity>
{
    public SubTaskEntityValidator()
    {
        RuleFor(x => x.ParentTaskId)
            .NotEmpty().WithMessage("Subtask must be associated with a valid parent Task.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Subtask title is required.")
            .MaximumLength(200).WithMessage("Subtask title cannot exceed 200 characters.");

        RuleFor(x => x.DueDate)
            .NotEmpty().WithMessage("Subtask due date is required.");

        RuleFor(x => x)
            .Must(x => !x.BlockedBySubTaskId.HasValue || x.BlockedBySubTaskId.Value != x.Id)
            .WithMessage("A subtask cannot be blocked by itself.");
    }
}
