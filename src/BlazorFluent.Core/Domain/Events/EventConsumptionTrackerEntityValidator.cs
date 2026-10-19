using BlazorFluent.Shared.DataListTypes;
using FluentValidation;

namespace BlazorFluent.Core.Domain.Events;

/// <summary>
/// Tier 2 Entity Validator for <see cref="EventConsumptionTrackerEntity"/>.
/// Enforces immutable event ledger consumption invariants before database persistence.
/// Executed automatically by EntityValidationInterceptor during SaveChangesAsync.
/// </summary>
public class EventConsumptionTrackerEntityValidator : AbstractValidator<EventConsumptionTrackerEntity>
{
    public EventConsumptionTrackerEntityValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("TenantId is required for event consumption isolation.");

        RuleFor(x => x.EventPublishTrackerId)
            .NotEmpty().WithMessage("EventPublishTrackerId is required.");

        RuleFor(x => x.CorrelationId)
            .NotEmpty().WithMessage("CorrelationId is required.")
            .MaximumLength(200).WithMessage("CorrelationId cannot exceed 200 characters.");

        RuleFor(x => x.ConsumerClass)
            .NotEmpty().WithMessage("ConsumerClass is required.")
            .MaximumLength(200).WithMessage("ConsumerClass cannot exceed 200 characters.");

        RuleFor(x => x.ConsumerMethod)
            .NotEmpty().WithMessage("ConsumerMethod is required.")
            .MaximumLength(200).WithMessage("ConsumerMethod cannot exceed 200 characters.");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("A valid EventConsumptionStatus is required.");

        RuleFor(x => x.AttemptCount)
            .GreaterThanOrEqualTo(1).WithMessage("AttemptCount must be at least 1.");

        RuleFor(x => x.DurationMs)
            .GreaterThanOrEqualTo(0).WithMessage("DurationMs cannot be negative.");
    }
}
