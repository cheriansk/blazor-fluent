using FluentValidation;

namespace BlazorFluent.Core.Domain.Tasks.Validators;

public class TaskEntityValidator : BaseTaskEntityValidator<TaskEntity>
{
    public TaskEntityValidator()
    {
        // Workflow sequencing invariants
        When(x => x.WorkflowId.HasValue && x.WorkflowId.Value != Guid.Empty, () =>
        {
            RuleFor(x => x.WorkflowStepOrder)
                .NotNull().WithMessage("WorkflowStepOrder is mandatory when the task belongs to a workflow.")
                .GreaterThan(0).WithMessage("WorkflowStepOrder must be a positive integer greater than zero.");
        });

        When(x => !x.WorkflowId.HasValue || x.WorkflowId.Value == Guid.Empty, () =>
        {
            RuleFor(x => x.WorkflowStepOrder)
                .Null().WithMessage("WorkflowStepOrder must be null when the task does not belong to a workflow.");
        });
    }
}
