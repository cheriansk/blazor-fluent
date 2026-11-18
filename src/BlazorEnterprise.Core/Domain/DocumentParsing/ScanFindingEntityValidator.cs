using FluentValidation;

namespace BlazorEnterprise.Core.Domain.DocumentParsing;

/// <summary>
/// FluentValidation validator for <see cref="ScanFindingEntity"/> (<c>TG009</c>).
/// </summary>
public sealed class ScanFindingEntityValidator : AbstractValidator<ScanFindingEntity>
{
    public ScanFindingEntityValidator()
    {
        RuleFor(x => x.ImportFileId)
            .NotEmpty().WithMessage("ImportFileId is required.");

        RuleFor(x => x.DocumentType)
            .NotEmpty().WithMessage("DocumentType is required.")
            .MaximumLength(100);

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.")
            .MaximumLength(4000);

        RuleFor(x => x.CustomerName)
            .MaximumLength(300);

        RuleFor(x => x.SourceVersion)
            .MaximumLength(100);

        RuleFor(x => x.TargetVersion)
            .MaximumLength(100);

        RuleFor(x => x.ProjectManager)
            .MaximumLength(200);

        RuleFor(x => x.Comment)
            .MaximumLength(4000);
    }
}
