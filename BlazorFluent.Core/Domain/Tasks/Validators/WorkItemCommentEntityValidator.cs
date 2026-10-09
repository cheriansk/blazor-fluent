using FluentValidation;

namespace BlazorFluent.Core.Domain.Tasks.Validators;

public class WorkItemCommentEntityValidator : AbstractValidator<WorkItemCommentEntity>
{
    public WorkItemCommentEntityValidator()
    {
        RuleFor(x => x.TargetItemId)
            .NotEmpty().WithMessage("Target work item ID is required.");

        RuleFor(x => x.CommentText)
            .NotEmpty().WithMessage("Comment text cannot be empty.")
            .MaximumLength(10000).WithMessage("Comment text cannot exceed 10,000 characters.");

        RuleFor(x => x.AuthorName)
            .MaximumLength(200).WithMessage("Author name cannot exceed 200 characters.");

        RuleFor(x => x.AuthorEmail)
            .MaximumLength(256).WithMessage("Author email cannot exceed 256 characters.");
    }
}
