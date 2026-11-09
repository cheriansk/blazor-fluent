using FluentValidation;

namespace BlazorEnterprise.Core.Domain.Sprints.Validators;

public class SprintEntityValidator : AbstractValidator<SprintEntity>
{
    public SprintEntityValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.SprintName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.SprintNumber).GreaterThan(0);
        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate)
            .WithMessage("Sprint EndDate must be strictly after StartDate.");
    }
}

public class SprintItemVersionEntityValidator : AbstractValidator<SprintItemVersionEntity>
{
    public SprintItemVersionEntityValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.ItemId).NotEmpty();
        RuleFor(x => x.EstimatedHours).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ActualHours).GreaterThanOrEqualTo(0);
    }
}

public class SprintAuditLogEntityValidator : AbstractValidator<SprintAuditLogEntity>
{
    public SprintAuditLogEntityValidator()
    {
        RuleFor(x => x.SprintId).NotEmpty();
        RuleFor(x => x.ChangeType).NotEmpty().MaximumLength(50);
    }
}
