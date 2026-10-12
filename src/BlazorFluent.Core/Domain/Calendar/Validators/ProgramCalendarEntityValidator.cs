using FluentValidation;

namespace BlazorFluent.Core.Domain.Calendar.Validators;

public class ProgramCalendarEntityValidator : AbstractValidator<ProgramCalendarEntity>
{
    public ProgramCalendarEntityValidator()
    {
        RuleFor(x => x.ProgramId)
            .NotEmpty().WithMessage("Program ID is required.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Event title is required.")
            .MaximumLength(200).WithMessage("Title cannot exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(5000).WithMessage("Description cannot exceed 5,000 characters.");

        RuleFor(x => x.StartUtc)
            .NotEmpty().WithMessage("Event start time is required.");

        RuleFor(x => x.EndUtc)
            .NotEmpty().WithMessage("Event end time is required.")
            .GreaterThanOrEqualTo(x => x.StartUtc).WithMessage("Event end time cannot be before start time.");

        RuleFor(x => x.LocationOrMeetingUrl)
            .MaximumLength(2000).WithMessage("Location or meeting URL cannot exceed 2,000 characters.");
    }
}
