using FluentValidation;

namespace BlazorEnterprise.Core.Domain.Resources.Validators;

public class ProjectResourceEntityValidator : AbstractValidator<ProjectResourceEntity>
{
    public ProjectResourceEntityValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.UserEntityId).NotEmpty();
        RuleFor(x => x.Role).IsInEnum();
    }
}

public class ResourceAllocationEntityValidator : AbstractValidator<ResourceAllocationEntity>
{
    public ResourceAllocationEntityValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.ProjectResourceId).NotEmpty();
        RuleFor(x => x.AllocationPercentage)
            .InclusiveBetween(0, 100)
            .WithMessage("Allocation percentage must be between 0 and 100.");
    }
}

public class ResourceRateEntityValidator : AbstractValidator<ResourceRateEntity>
{
    public ResourceRateEntityValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.ProjectResourceId).NotEmpty();
        RuleFor(x => x.HourlyRate)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Hourly rate must be non-negative.");
        RuleFor(x => x.CurrencyCode)
            .NotEmpty()
            .MaximumLength(10);
    }
}
