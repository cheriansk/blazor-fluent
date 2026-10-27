using FluentValidation;

namespace BlazorEnterprise.Core.Domain.Knowledge;

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

        RuleFor(x => x.UserEntityId)
            .NotEmpty().WithMessage("UserEntityId is required.");

        When(x => x.HasApproved, () =>
        {
            RuleFor(x => x.ApprovedAtUtc)
                .NotNull().WithMessage("ApprovedAtUtc must be set when reviewer has approved the article.");
        });
    }
}
