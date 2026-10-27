using BlazorEnterprise.Shared.DataListTypes;
using FluentValidation;

namespace BlazorEnterprise.Core.Domain.Auditing;

/// <summary>
/// Tier 2 Entity Validator for <see cref="AuditRecordEntity"/>.
/// Enforces forensic integrity rules and event-specific invariants across the heterogeneous audit stream.
/// Executed automatically by EntityValidationInterceptor before database persistence.
/// </summary>
public class AuditRecordEntityValidator : AbstractValidator<AuditRecordEntity>
{
    public AuditRecordEntityValidator()
    {
        // 1. Universal Base Invariants
        RuleFor(x => x.TenantId)
            .NotEmpty()
            .WithMessage("TenantId is required for forensic audit isolation.");

        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Audit record Id must be a valid unique identifier.");

        RuleFor(x => x.EventType)
            .IsInEnum()
            .WithMessage("A valid AuditEventType is required.");

        RuleFor(x => x.Severity)
            .IsInEnum()
            .WithMessage("A valid AuditSeverity is required.");

        // 2. Conditional Invariants: EntityChange Events
        When(x => x.EventType == AuditEventTypeEnum.EntityChange, () =>
        {
            RuleFor(x => x.EntityName)
                .NotEmpty()
                .WithMessage("EntityName is required for entity change audit records.");

            RuleFor(x => x.EntityId)
                .NotEmpty()
                .WithMessage("EntityId is required for entity change audit records.");

            RuleFor(x => x.Operation)
                .NotNull()
                .WithMessage("Operation is required for entity change audit records.")
                .IsInEnum()
                .WithMessage("A valid EntityOperation (Insert, Update, Delete) is required.");

            RuleFor(x => x.ChangesJson)
                .NotEmpty()
                .WithMessage("ChangesJson property diff is required for entity change audit records.");
        });

        // 3. Conditional Invariants: Non-Entity Events (Security, TenantSwitch, UserActivity, Exception)
        When(x => x.EventType != AuditEventTypeEnum.EntityChange, () =>
        {
            RuleFor(x => x.Description)
                .NotEmpty()
                .WithMessage("Description is required for security, tenant-switch, and activity audit events.");
        });

        // 4. User Attribution Invariants
        When(x => x.UserType.HasValue, () =>
        {
            RuleFor(x => x.UserId)
                .NotEmpty()
                .WithMessage("UserId is required when UserType is specified.");

            RuleFor(x => x.UserEmail)
                .NotEmpty()
                .WithMessage("UserEmail is required when UserType is specified.");
        });

        When(x => !string.IsNullOrWhiteSpace(x.UserEmail), () =>
        {
            RuleFor(x => x.UserEmail)
                .Must(email => email!.Equals("system", StringComparison.OrdinalIgnoreCase) ||
                               (email!.Contains('@') && email!.Contains('.')))
                .WithMessage("UserEmail must be 'system' or a valid email address.");
        });
    }
}
