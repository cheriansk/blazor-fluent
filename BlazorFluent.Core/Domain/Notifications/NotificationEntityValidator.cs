using FluentValidation;

namespace BlazorFluent.Core.Domain.Notifications;

/// <summary>
/// Tier 2 Entity Validator for NotificationEntity.
/// Enforces single-entity invariants before persistence.
/// Executed automatically by EntityValidationInterceptor during SaveChangesAsync.
/// </summary>
public class NotificationEntityValidator : AbstractValidator<NotificationEntity>
{
    public NotificationEntityValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Notification title is required.")
            .MaximumLength(200).WithMessage("Notification title cannot exceed 200 characters.");

        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Notification message is required.")
            .MaximumLength(4000).WithMessage("Notification message cannot exceed 4000 characters.");

        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("Notification TenantId is mandatory.");
    }
}
