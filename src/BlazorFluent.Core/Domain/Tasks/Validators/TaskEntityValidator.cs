using FluentValidation;

namespace BlazorFluent.Core.Domain.Tasks.Validators;

public class TaskEntityValidator : AbstractValidator<TaskEntity>
{
    public TaskEntityValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Task title is required.")
            .MaximumLength(200).WithMessage("Task title cannot exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(10000).WithMessage("Task description cannot exceed 10,000 characters.");

        RuleFor(x => x.DueDate)
            .NotEmpty().WithMessage("Task Due Date is required.");

        RuleFor(x => x.ReporterEmail)
            .MaximumLength(256).WithMessage("Reporter email cannot exceed 256 characters.");

        RuleFor(x => x.AssigneeEmails)
            .MaximumLength(2000).WithMessage("Assignee emails string cannot exceed 2,000 characters.");

        RuleFor(x => x.Labels)
            .MaximumLength(500).WithMessage("Labels string cannot exceed 500 characters.");

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
