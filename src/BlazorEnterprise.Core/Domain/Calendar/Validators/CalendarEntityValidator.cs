using FluentValidation;

namespace BlazorEnterprise.Core.Domain.Calendar.Validators;

public class CalendarEntityValidator : AbstractValidator<CalendarEntity>
{
    public CalendarEntityValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("TenantId is mandatory.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title cannot exceed 200 characters.");

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("StartDate is required.");

        When(x => x.EndDate.HasValue, () =>
        {
            RuleFor(x => x.EndDate!.Value)
                .GreaterThanOrEqualTo(x => x.StartDate)
                .WithMessage("EndDate cannot precede StartDate.");
        });
    }
}
