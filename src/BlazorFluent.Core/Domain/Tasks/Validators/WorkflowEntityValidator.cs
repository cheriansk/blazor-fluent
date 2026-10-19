using FluentValidation;

namespace BlazorFluent.Core.Domain.Tasks.Validators;

public class WorkflowEntityValidator : AbstractValidator<WorkflowEntity>
{
    public WorkflowEntityValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Workflow title is required.")
            .MaximumLength(200).WithMessage("Workflow title cannot exceed 200 characters.");

        RuleFor(x => x.WorkflowOwnerEmail)
            .NotEmpty().WithMessage("Workflow Owner email is required.")
            .MaximumLength(256).WithMessage("Workflow Owner email cannot exceed 256 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(10000).WithMessage("Workflow description cannot exceed 10,000 characters.");

        RuleFor(x => x.DueDate)
            .NotEmpty().WithMessage("Workflow Due Date is required.");

        RuleFor(x => x.ReporterEmail)
            .MaximumLength(256).WithMessage("Reporter email cannot exceed 256 characters.");

        RuleFor(x => x.WatcherEmails)
            .MaximumLength(2000).WithMessage("Watcher emails string cannot exceed 2,000 characters.");

        RuleFor(x => x.Labels)
            .MaximumLength(500).WithMessage("Labels string cannot exceed 500 characters.");
    }
}
