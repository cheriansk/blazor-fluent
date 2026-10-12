using FluentValidation;

namespace BlazorFluent.Core.Domain.Tasks.Validators;

public class RiskEntityValidator : AbstractValidator<RiskEntity>
{
    public RiskEntityValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Risk title is required.")
            .MaximumLength(200).WithMessage("Risk title cannot exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(10000).WithMessage("Risk description cannot exceed 10,000 characters.");

        RuleFor(x => x.MitigationStrategy)
            .MaximumLength(5000).WithMessage("Mitigation strategy cannot exceed 5,000 characters.");

        RuleFor(x => x.ContingencyPlan)
            .MaximumLength(5000).WithMessage("Contingency plan cannot exceed 5,000 characters.");

        RuleFor(x => x.DueDate)
            .NotEmpty().WithMessage("Risk target review/due date is required.");
    }
}
