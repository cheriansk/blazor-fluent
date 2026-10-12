using FluentValidation;

namespace BlazorFluent.Core.Domain.Tasks.Validators;

public class WorkflowStepEntityValidator : AbstractValidator<WorkflowStepEntity>
{
    public WorkflowStepEntityValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Workflow step title is required.")
            .MaximumLength(200).WithMessage("Workflow step title cannot exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(10000).WithMessage("Workflow step description cannot exceed 10,000 characters.");

        RuleFor(x => x.StepOrder)
            .GreaterThan(0).WithMessage("Workflow step order must be greater than zero.");

        RuleFor(x => x.StepOwnerEmail)
            .NotEmpty().WithMessage("Step owner email is required.")
            .MaximumLength(256).WithMessage("Step owner email cannot exceed 256 characters.");

        RuleFor(x => x.CompletionNotes)
            .MaximumLength(4000).WithMessage("Completion notes cannot exceed 4,000 characters.");
    }
}
