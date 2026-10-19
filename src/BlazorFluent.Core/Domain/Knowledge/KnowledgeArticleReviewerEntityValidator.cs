using FluentValidation;

namespace BlazorFluent.Core.Domain.Knowledge;

/// <summary>
/// Tier 2 Entity Validator for <see cref="KnowledgeArticleReviewerEntity"/>.
/// Enforces reviewer assignment and approval state invariants before database persistence.
/// Executed automatically by EntityValidationInterceptor during SaveChangesAsync.
/// </summary>
public class KnowledgeArticleReviewerEntityValidator : AbstractValidator<KnowledgeArticleReviewerEntity>
{
    public KnowledgeArticleReviewerEntityValidator()
    {
        RuleFor(x => x.ArticleId)
            .NotEmpty().WithMessage("ArticleId is required for knowledge article reviewer.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.")
            .MaximumLength(200).WithMessage("UserId cannot exceed 200 characters.");

        RuleFor(x => x.UserEmail)
            .NotEmpty().WithMessage("User email is required.")
            .EmailAddress().WithMessage("User email must be a valid email address.")
            .MaximumLength(256).WithMessage("User email cannot exceed 256 characters.");

        RuleFor(x => x.UserName)
            .NotEmpty().WithMessage("User name is required.")
            .MaximumLength(200).WithMessage("User name cannot exceed 200 characters.");

        When(x => x.HasApproved, () =>
        {
            RuleFor(x => x.ApprovedAtUtc)
                .NotNull().WithMessage("ApprovedAtUtc must be set when reviewer has approved the article.");
        });
    }
}
