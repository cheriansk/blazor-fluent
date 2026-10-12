using FluentValidation;

namespace BlazorFluent.Core.Domain.Tasks.Validators;

public class BlockerDependencyEntityValidator : AbstractValidator<BlockerDependencyEntity>
{
    public BlockerDependencyEntityValidator()
    {
        RuleFor(x => x.SourceTaskId)
            .NotEmpty().WithMessage("Source task (waiting task) ID is required.");

        RuleFor(x => x.TargetTaskId)
            .NotEmpty().WithMessage("Target task (blocking task) ID is required.");

        RuleFor(x => x)
            .Must(x => x.SourceTaskId != x.TargetTaskId)
            .WithMessage("A task cannot block itself.");

        RuleFor(x => x.ImpactDetails)
            .NotEmpty().WithMessage("Impact details describing consequences of unresolved blocker are mandatory.")
            .MaximumLength(5000).WithMessage("Impact details cannot exceed 5,000 characters.");

        RuleFor(x => x.ResolveDueDate)
            .NotEmpty().WithMessage("Resolve SLA due date is required.");
    }
}
