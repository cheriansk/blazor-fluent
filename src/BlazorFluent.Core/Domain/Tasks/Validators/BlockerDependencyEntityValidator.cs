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

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description / impact details describing consequences of unresolved blocker are mandatory.")
            .MaximumLength(10000).WithMessage("Description cannot exceed 10,000 characters.");

        RuleFor(x => x.DueDate)
            .NotEmpty().WithMessage("Resolve SLA due date is required.");
    }
}
