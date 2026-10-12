using FluentValidation;

namespace BlazorFluent.Core.Domain.Knowledge;

/// <summary>
/// Tier-2 domain validator for <see cref="KnowledgeArticleEntity"/>.
/// Enforces business constraints before persistence in AppDbContext.
/// </summary>
public class KnowledgeArticleEntityValidator : AbstractValidator<KnowledgeArticleEntity>
{
    public KnowledgeArticleEntityValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Article title is required.")
            .MaximumLength(100).WithMessage("Article title cannot exceed 100 characters.");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Article content is required.")
            .MaximumLength(20000).WithMessage("Article content cannot exceed 20,000 characters.");

        RuleFor(x => x.Labels)
            .NotEmpty().WithMessage("At least 1 label is required.")
            .Must(labels =>
            {
                if (string.IsNullOrWhiteSpace(labels)) return false;
                var count = labels.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
                return count >= 1 && count <= 10;
            }).WithMessage("An article must contain between 1 and 10 labels.");

        RuleFor(x => x.AuthorUserId)
            .NotEmpty().WithMessage("Author user ID is required.");
    }
}
