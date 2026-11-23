// [NEED-HUMAN-REVIEW]
using FluentValidation;

namespace BlazorEnterprise.Core.Domain.Tenancy;

/// <summary>
/// FluentValidation validator for <see cref="ProgramTeamEntity"/> (<c>TG009</c>).
/// </summary>
public sealed class ProgramTeamEntityValidator : AbstractValidator<ProgramTeamEntity>
{
    public ProgramTeamEntityValidator()
    {
        RuleFor(x => x.ProgramId)
            .NotEmpty().WithMessage("ProgramId is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Team name is required.")
            .MaximumLength(150);

        RuleFor(x => x.Description)
            .MaximumLength(1000);

        RuleFor(x => x.OwnerNames)
            .NotNull()
            .Must(o => o.Count >= 1 && o.Count <= 4)
            .WithMessage("Each team must have between 1 and 4 owner names.");

        RuleForEach(x => x.OwnerNames)
            .NotEmpty().WithMessage("Owner name cannot be empty.")
            .MaximumLength(150);
    }
}
