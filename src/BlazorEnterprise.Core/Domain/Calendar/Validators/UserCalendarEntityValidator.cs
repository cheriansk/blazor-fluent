using BlazorEnterprise.Shared.DataListTypes;
using FluentValidation;

namespace BlazorEnterprise.Core.Domain.Calendar.Validators;

public class UserCalendarEntityValidator : AbstractValidator<UserCalendarEntity>
{
    public UserCalendarEntityValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title cannot exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(5000).WithMessage("Description cannot exceed 5,000 characters.");

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("Start date is required.");

        // When EntryType == Personal: Title, StartDate, Status, and DueDate are mandatory. EndDate is optional.
        When(x => x.EntryType == CalendarEntryTypeEnum.Personal, () =>
        {
            RuleFor(x => x.DueDate)
                .NotEmpty().WithMessage("Due date is required for personal calendar entries.");

            RuleFor(x => x.Status)
                .IsInEnum().WithMessage("Valid status is required.");

            RuleFor(x => x.EndDate)
                .GreaterThanOrEqualTo(x => x.StartDate)
                .When(x => x.EndDate.HasValue)
                .WithMessage("End date cannot be before start date.");
        });
    }
}
