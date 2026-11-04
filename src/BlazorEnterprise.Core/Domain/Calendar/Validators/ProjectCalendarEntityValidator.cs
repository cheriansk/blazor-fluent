using System;
using System.Linq;
using System.Text.RegularExpressions;
using BlazorEnterprise.Shared.DataListTypes;
using FluentValidation;

namespace BlazorEnterprise.Core.Domain.Calendar.Validators;

public class ProjectCalendarEntityValidator : AbstractValidator<ProjectCalendarEntity>
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public ProjectCalendarEntityValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage("Project ID is required.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title cannot exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(5000).WithMessage("Description cannot exceed 5,000 characters.");

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("Start date is required.");

        // When EntryType == Timeline: Title, StartDate, EndDate, Status are mandatory
        When(x => x.EntryType == CalendarEntryTypeEnum.Timeline, () =>
        {
            RuleFor(x => x.EndDate)
                .NotNull().WithMessage("End date is required for Timeline entries.")
                .GreaterThanOrEqualTo(x => x.StartDate).WithMessage("End date cannot be before start date.");

            RuleFor(x => x.Status)
                .IsInEnum().WithMessage("Valid status is required.");
        });

        // When EntryType == Milestone: Title, StartDate, EndDate, Status, OwnerEmails are mandatory
        When(x => x.EntryType == CalendarEntryTypeEnum.Milestone, () =>
        {
            RuleFor(x => x.EndDate)
                .NotNull().WithMessage("End date is required for Milestone entries.")
                .GreaterThanOrEqualTo(x => x.StartDate).WithMessage("End date cannot be before start date.");

            RuleFor(x => x.Status)
                .IsInEnum().WithMessage("Valid status is required.");

            RuleFor(x => x.OwnerEmails)
                .NotEmpty().WithMessage("At least one owner email is required for Milestone entries.")
                .Must(ValidateSemicolonSeparatedEmails).WithMessage("One or more owner emails are invalid.");
        });
    }

    private static bool ValidateSemicolonSeparatedEmails(string? ownerEmails)
    {
        if (string.IsNullOrWhiteSpace(ownerEmails)) return false;
        var parts = ownerEmails.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return false;
        return parts.All(p => EmailRegex.IsMatch(p));
    }
}
