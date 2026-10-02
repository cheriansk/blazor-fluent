using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.DTOs;
using FluentValidation;

namespace BlazorFluent.Core.Validation;

/// <summary>
/// Tier-1 Page/DTO validator for creating knowledge base articles.
/// Enforces mandatory title, rich content length, explicit scope selection (no default),
/// and mandatory manual labels (1 to 10 labels).
/// </summary>
public class CreateArticleDtoValidator : AbstractValidator<CreateArticleDto>
{
    public CreateArticleDtoValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Article title is required.")
            .MaximumLength(100).WithMessage("Article title cannot exceed 100 characters.");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Article description/content is required.")
            .MaximumLength(20000).WithMessage("Article content cannot exceed 20,000 characters.");

        RuleFor(x => x.Scope)
            .NotNull().WithMessage("Please select article scope (Tenant Only or Global).")
            .IsInEnum().WithMessage("Invalid article scope selection.");

        RuleFor(x => x.Labels)
            .NotNull().WithMessage("At least 1 label is required.")
            .Must(labels => labels != null && labels.Count >= 1 && labels.Count <= 10)
            .WithMessage("You must add between 1 and 10 labels for the article.");

        RuleFor(x => x.ReviewerUserIds)
            .NotNull().WithMessage("At least 1 reviewer must be assigned.")
            .Must(reviewers => reviewers != null && reviewers.Count >= 1)
            .WithMessage("Please select at least 1 reviewer to review and approve this article.");
    }
}
