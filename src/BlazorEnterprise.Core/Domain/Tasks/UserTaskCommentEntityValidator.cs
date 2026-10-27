using FluentValidation;

namespace BlazorEnterprise.Core.Domain.Tasks;

/// <summary>
/// Tier 2 Entity Invariant Validator for <see cref="UserTaskCommentEntity"/>.
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

        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage("Comment ProjectId is mandatory.");

        RuleFor(x => x.AuthorUserEntityId)
            .NotEmpty().WithMessage("Comment AuthorUserEntityId is mandatory.");
    }
}
