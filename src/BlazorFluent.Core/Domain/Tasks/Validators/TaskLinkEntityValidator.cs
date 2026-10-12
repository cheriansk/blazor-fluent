using FluentValidation;

namespace BlazorFluent.Core.Domain.Tasks.Validators;

public class TaskLinkEntityValidator : AbstractValidator<TaskLinkEntity>
{
    public TaskLinkEntityValidator()
    {
        RuleFor(x => x.SourceTaskId)
            .NotEmpty().WithMessage("Source task ID is required.");

        RuleFor(x => x.TargetTaskId)
            .NotEmpty().WithMessage("Target task ID is required.");

        RuleFor(x => x)
            .Must(x => x.SourceTaskId != x.TargetTaskId)
            .WithMessage("A task cannot link to itself.");
    }
}
