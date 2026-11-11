using FluentValidation;

namespace BlazorEnterprise.Core.Domain.Tasks.Validators;

/// <summary>
/// Tier 2 entity validator for <see cref="DeliverableEntity"/>.
/// Inherits common work item invariants from <see cref="BaseTaskEntityValidator{T}"/>.
/// </summary>
public class DeliverableEntityValidator : BaseTaskEntityValidator<DeliverableEntity>
{
    public DeliverableEntityValidator()
    {
        RuleFor(x => x.ParentType)
            .IsInEnum().WithMessage("A valid ParentType is mandatory for deliverables.");

        RuleFor(x => x.ParentItemId)
            .NotEmpty().WithMessage("ParentItemId must not be empty.");

        RuleFor(x => x.SignOffStatus)
            .IsInEnum().WithMessage("A valid SignOffStatus is mandatory for deliverables.");

        RuleFor(x => x.ArtifactUrl)
            .MaximumLength(2000).WithMessage("Artifact URL cannot exceed 2000 characters.");

        RuleFor(x => x.SignOffNotes)
            .MaximumLength(5000).WithMessage("Sign-off notes cannot exceed 5000 characters.");

        RuleFor(x => x.ApprovedBy)
            .MaximumLength(256).WithMessage("ApprovedBy email cannot exceed 256 characters.");

        When(x => x.SignOffStatus == BlazorEnterprise.Shared.DataListTypes.DeliverableStatusEnum.Approved, () =>
        {
            RuleFor(x => x.ApprovedBy)
                .NotEmpty().WithMessage("ApprovedBy is required when deliverable is marked Approved.");

            RuleFor(x => x.ApprovedAtUtc)
                .NotNull().WithMessage("ApprovedAtUtc is required when deliverable is marked Approved.");
        });

        When(x => x.SignOffStatus == BlazorEnterprise.Shared.DataListTypes.DeliverableStatusEnum.Rejected, () =>
        {
            RuleFor(x => x.SignOffNotes)
                .NotEmpty().WithMessage("Sign-off rejection notes are mandatory when deliverable is rejected.");
        });
    }
}
