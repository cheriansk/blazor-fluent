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
    }
}
