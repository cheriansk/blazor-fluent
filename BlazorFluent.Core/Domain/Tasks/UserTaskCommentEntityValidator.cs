using FluentValidation;

namespace BlazorFluent.Core.Domain.Tasks;

/// <summary>
/// Tier 2 Entity Invariant Validator for TaskCommentEntity.
/// Validates pure in-memory comment constraints before database persistence.
/// Executed automatically by EntityValidationInterceptor during SaveChangesAsync.
/// </summary>
public class UserTaskCommentEntityValidator : AbstractValidator<UserTaskCommentEntity>
{
    public UserTaskCommentEntityValidator()
    {
        RuleFor(x => x.CommentText)
            .NotEmpty().WithMessage("Comment text cannot be empty.")
            .MaximumLength(4000).WithMessage("Comment text cannot exceed 4,000 characters.");

        RuleFor(x => x.TaskId)
            .NotEmpty().WithMessage("Comment TaskId is mandatory.");

        RuleFor(x => x.AuthorName)
            .MaximumLength(200).WithMessage("Author name cannot exceed 200 characters.");

        RuleFor(x => x.AuthorEmail)
            .MaximumLength(256).WithMessage("Author email cannot exceed 256 characters.");
    }
}
