using FluentValidation;

namespace BlazorEnterprise.Core.Domain.Tasks.Validators;

public class WorkflowEntityValidator : BaseTaskEntityValidator<WorkflowEntity>
{
    public WorkflowEntityValidator()
    {
        RuleFor(x => x.AssigneeEmails)
            .NotEmpty().WithMessage("Workflow Owner email is required.")
            .MaximumLength(256).WithMessage("Workflow Owner email cannot exceed 256 characters.")
            .Must(emails => !string.IsNullOrEmpty(emails) && !emails.Contains(';') && !emails.Contains(','))
            .WithMessage("Workflow Owner must be a single email address, not multiple assignees.")
            .EmailAddress().WithMessage("Workflow Owner must be a valid email address.");
    }
}
