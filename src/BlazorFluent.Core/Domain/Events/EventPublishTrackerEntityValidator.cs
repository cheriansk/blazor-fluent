using BlazorFluent.Shared.DataListTypes;
using FluentValidation;

namespace BlazorFluent.Core.Domain.Events;

/// <summary>
/// Tier 2 Entity Validator for <see cref="EventPublishTrackerEntity"/>.
/// Enforces immutable event ledger publication invariants before database persistence.
/// Executed automatically by EntityValidationInterceptor during SaveChangesAsync.
/// </summary>
public class EventPublishTrackerEntityValidator : AbstractValidator<EventPublishTrackerEntity>
{
    public EventPublishTrackerEntityValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("TenantId is required for event publishing isolation.");

        RuleFor(x => x.EventId)
            .NotEmpty().WithMessage("EventId is required.");

        RuleFor(x => x.CorrelationId)
            .NotEmpty().WithMessage("CorrelationId is required.")
            .MaximumLength(200).WithMessage("CorrelationId cannot exceed 200 characters.");

        RuleFor(x => x.EventName)
            .NotEmpty().WithMessage("EventName is required.")
            .MaximumLength(200).WithMessage("EventName cannot exceed 200 characters.");

        RuleFor(x => x.SourceClass)
            .NotEmpty().WithMessage("SourceClass is required.")
            .MaximumLength(200).WithMessage("SourceClass cannot exceed 200 characters.");

        RuleFor(x => x.SourceMethod)
            .NotEmpty().WithMessage("SourceMethod is required.")
            .MaximumLength(200).WithMessage("SourceMethod cannot exceed 200 characters.");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("A valid EventTrackerStatus is required.");

        RuleFor(x => x.ConsumerCount)
            .GreaterThanOrEqualTo(0).WithMessage("ConsumerCount cannot be negative.");
    }
}
