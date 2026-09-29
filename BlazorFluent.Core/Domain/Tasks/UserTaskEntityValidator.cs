using FluentValidation;

namespace BlazorFluent.Core.Domain.Tasks;

/// <summary>
/// Tier 2 Entity Invariant Validator for UserTaskEntity.
/// Validates pure in-memory entity constraints before database persistence.
/// Executed automatically by EntityValidationInterceptor during SaveChangesAsync.
/// </summary>
public class UserTaskEntityValidator : AbstractValidator<UserTaskEntity>
{
    public UserTaskEntityValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Task title is required.")
            .MaximumLength(200).WithMessage("Task title cannot exceed 200 characters.");

        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage("Task ProjectId is mandatory.");

        RuleFor(x => x.Description)
            .MaximumLength(10000).WithMessage("Task description cannot exceed 10,000 characters.");

        RuleFor(x => x.AssigneeEmails)
            .MaximumLength(2000).WithMessage("Assignee emails string cannot exceed 2,000 characters.");

        RuleFor(x => x.Labels)
            .MaximumLength(500).WithMessage("Labels string cannot exceed 500 characters.");
    }
}
