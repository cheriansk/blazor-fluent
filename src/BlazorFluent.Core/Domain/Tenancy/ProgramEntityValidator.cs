using FluentValidation;

namespace BlazorFluent.Core.Domain.Tenancy;

/// <summary>
/// Tier 2 Entity Invariant Validator for ProgramEntity.
/// Enforces business constraints before persistence in PostgreSQL.
/// </summary>
public class ProgramEntityValidator : AbstractValidator<ProgramEntity>
{
    public ProgramEntityValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("TenantId is mandatory for all program records.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Program name is required.")
            .MaximumLength(150).WithMessage("Program name cannot exceed 150 characters.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Program code is required.")
            .MaximumLength(50).WithMessage("Program code cannot exceed 50 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Program description cannot exceed 2,000 characters.");

        RuleFor(x => x.DueDate)
            .GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage("Program due date cannot precede start date.");

        When(x => x.EndDate.HasValue, () =>
        {
            RuleFor(x => x.EndDate!.Value)
                .GreaterThanOrEqualTo(x => x.StartDate)
                .WithMessage("Program target end date cannot precede start date.");
        });

        When(x => x.RevisedDueDate.HasValue, () =>
        {
            RuleFor(x => x.RevisedDueDate!.Value)
                .GreaterThanOrEqualTo(x => x.StartDate)
                .WithMessage("Program revised due date cannot precede start date.");
        });
    }
}
