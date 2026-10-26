using FluentValidation;

namespace BlazorEnterprise.Core.Domain.Calendar.Validators;

public class ProjectCalendarEntityValidator : AbstractValidator<ProjectCalendarEntity>
{
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

        RuleFor(x => x.EndDate)
            .Must((entity, endDate) => !endDate.HasValue || endDate.Value.Date >= entity.StartDate.Date)
            .WithMessage("End date cannot precede start date.");
    }
}
