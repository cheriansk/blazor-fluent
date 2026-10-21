using BlazorFluent.Shared.DataListTypes;
using FluentValidation;

namespace BlazorFluent.Core.Domain.Jobs;

/// <summary>
/// Tier 2 Entity Validator for <see cref="JobExecutionEntity"/>.
/// Enforces background job execution state and diagnostic invariants before database persistence.
/// Executed automatically by EntityValidationInterceptor during SaveChangesAsync.
/// </summary>
public class JobExecutionEntityValidator : AbstractValidator<JobExecutionEntity>
{
    public JobExecutionEntityValidator()
    {
        RuleFor(x => x.CorrelationId)
            .NotEmpty().WithMessage("CorrelationId is required for distributed traceability.")
            .MaximumLength(100).WithMessage("CorrelationId cannot exceed 100 characters.");

        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("TenantId is required.");

        RuleFor(x => x.JobName)
            .NotEmpty().WithMessage("JobName is required.")
            .MaximumLength(200).WithMessage("JobName cannot exceed 200 characters.");

        RuleFor(x => x.TriggerSource)
            .IsInEnum().WithMessage("A valid JobTriggerSource is required.");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("A valid JobStatus is required.");

        RuleFor(x => x.AttemptCount)
            .GreaterThanOrEqualTo(1).WithMessage("AttemptCount must be at least 1.");

        RuleFor(x => x.MaxRetries)
            .GreaterThanOrEqualTo(1).WithMessage("MaxRetries must be at least 1.");

        When(x => x.Status is JobStatusEnum.Succeeded or JobStatusEnum.Failed, () =>
        {
            RuleFor(x => x.CompletedAt)
                .NotNull().WithMessage("CompletedAt must be recorded when job execution terminates.");
        });
    }
}
