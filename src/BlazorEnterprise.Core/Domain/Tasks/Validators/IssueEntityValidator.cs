using FluentValidation;

namespace BlazorEnterprise.Core.Domain.Tasks.Validators;

public class IssueEntityValidator : AbstractValidator<IssueEntity>
{
    public IssueEntityValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Issue title is required.")
            .MaximumLength(200).WithMessage("Issue title cannot exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(10000).WithMessage("Issue description cannot exceed 10,000 characters.");

        RuleFor(x => x.RootCause)
            .MaximumLength(5000).WithMessage("Root cause cannot exceed 5,000 characters.");

        RuleFor(x => x.Workaround)
            .MaximumLength(5000).WithMessage("Workaround cannot exceed 5,000 characters.");

        RuleFor(x => x.ResolutionStrategy)
            .MaximumLength(5000).WithMessage("Resolution strategy cannot exceed 5,000 characters.");

        RuleFor(x => x.DueDate)
            .NotEmpty().WithMessage("Issue target resolution date is required.");
    }
}
