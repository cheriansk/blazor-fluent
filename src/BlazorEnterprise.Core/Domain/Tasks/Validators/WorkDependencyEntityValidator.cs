using BlazorEnterprise.Shared.DataListTypes;
using FluentValidation;

namespace BlazorEnterprise.Core.Domain.Tasks.Validators;

/// <summary>
/// Tier 2 Entity Invariant Validator for <see cref="WorkDependencyEntity"/>.
/// </summary>
public class WorkDependencyEntityValidator : AbstractValidator<WorkDependencyEntity>
{
    public WorkDependencyEntityValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Dependency headline title is required.")
            .MaximumLength(500).WithMessage("Title cannot exceed 500 characters.");

        RuleFor(x => x.DependentItemId)
            .Must((entity, id) => entity.DependentType == DependencyNodeTypeEnum.ManualDependency || (id.HasValue && id.Value != Guid.Empty))
            .WithMessage("Dependent item ID is required when DependentType is CalendarGate or TaskItem.");

        RuleFor(x => x.DependsOnItemId)
            .Must((entity, id) => entity.DependsOnType == DependencyNodeTypeEnum.ManualDependency || (id.HasValue && id.Value != Guid.Empty))
            .WithMessage("Prerequisite item ID is required when DependsOnType is CalendarGate or TaskItem.");

        RuleFor(x => x)
            .Must(x => !(x.DependentType == x.DependsOnType &&
                         x.DependentItemId.HasValue &&
                         x.DependsOnItemId.HasValue &&
                         x.DependentItemId.Value == x.DependsOnItemId.Value))
            .WithMessage("An item cannot depend on itself.");

        RuleFor(x => x.Description)
            .MaximumLength(10000).WithMessage("Description / impact details cannot exceed 10,000 characters.");

        RuleFor(x => x.DueDate)
            .NotEmpty().WithMessage("Target resolution due date is required.");
    }
}
