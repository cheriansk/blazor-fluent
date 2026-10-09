using FluentValidation;

namespace BlazorFluent.Core.Domain.Tasks.Validators;

public class AssumptionEntityValidator : AbstractValidator<AssumptionEntity>
{
    public AssumptionEntityValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Assumption title is required.")
            .MaximumLength(200).WithMessage("Assumption title cannot exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(10000).WithMessage("Assumption description cannot exceed 10,000 characters.");

        RuleFor(x => x.ValidationCriteria)
            .MaximumLength(5000).WithMessage("Validation criteria cannot exceed 5,000 characters.");

        RuleFor(x => x.ImpactIfInvalid)
            .MaximumLength(5000).WithMessage("Impact if invalid cannot exceed 5,000 characters.");

        RuleFor(x => x.DueDate)
            .NotEmpty().WithMessage("Assumption target validation date is required.");
    }
}
