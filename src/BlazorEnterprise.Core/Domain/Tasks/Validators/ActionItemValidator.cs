using FluentValidation;

namespace BlazorEnterprise.Core.Domain.Tasks.Validators;

/// <summary>
/// Domain validator for <see cref="ActionItemEntity"/>.
/// </summary>
public class ActionItemValidator : AbstractValidator<ActionItemEntity>
{
    public ActionItemValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Action item title is required.")
            .MaximumLength(500).WithMessage("Action item title cannot exceed 500 characters.");

        RuleFor(x => x.ParentTaskId)
            .NotEmpty().WithMessage("ParentTaskId is required.");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Description cannot exceed 2000 characters.")
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.Complexity)
            .InclusiveBetween(1, 5).WithMessage("Complexity must be between 1 and 5 points.");

        RuleFor(x => x.EstimatedHours)
            .GreaterThanOrEqualTo(0).WithMessage("Estimated hours cannot be negative.");

        RuleFor(x => x.OrderIndex)
            .GreaterThanOrEqualTo(0).WithMessage("Order index cannot be negative.");

        RuleFor(x => x.BlockingReason)
            .NotEmpty().WithMessage("Blocking reason is required when an action item is marked blocked.")
            .MinimumLength(10).WithMessage("Blocking reason must be at least 10 characters.")
            .MaximumLength(1000).WithMessage("Blocking reason cannot exceed 1000 characters.")
            .When(x => x.IsBlocked);
    }
}
