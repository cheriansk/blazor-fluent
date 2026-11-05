using FluentValidation;
using BlazorEnterprise.Shared.DataListTypes;

namespace BlazorEnterprise.Core.Domain.Versioning;

/// <summary>
/// Validator for RevisionHeaderEntity ensuring enterprise integrity and strict scope matrix rules.
/// </summary>
public class RevisionHeaderEntityValidator : AbstractValidator<RevisionHeaderEntity>
{
    public RevisionHeaderEntityValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("TenantId is required for scenario revisions.");

        RuleFor(x => x.VerNum)
            .GreaterThanOrEqualTo(1).WithMessage("Version number must be greater than or equal to 1.");

        RuleFor(x => x.RevisionName)
            .NotEmpty().WithMessage("RevisionName is required.")
            .MaximumLength(200).WithMessage("RevisionName must not exceed 200 characters.");

        RuleFor(x => x.CreatorEmail)
            .NotEmpty().WithMessage("CreatorEmail is required.")
            .MaximumLength(256).WithMessage("CreatorEmail must not exceed 256 characters.");

        RuleFor(x => x.ScopeLevel)
            .IsInEnum().WithMessage("A valid ScopeLevel is required.");

        RuleFor(x => x.FeatureType)
            .IsInEnum().WithMessage("A valid FeatureType is required.");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("A valid Status is required.");

        // Strict Scope Rules
        When(x => x.ScopeLevel == TaskLevelEnum.Tenant, () =>
        {
            RuleFor(x => x.ProgramId)
                .Must(id => !id.HasValue || id.Value == Guid.Empty)
                .WithMessage("ProgramId must be empty for Tenant-level revisions.");
            RuleFor(x => x.ProjectId)
                .Must(id => !id.HasValue || id.Value == Guid.Empty)
                .WithMessage("ProjectId must be empty for Tenant-level revisions.");
        });

        When(x => x.ScopeLevel == TaskLevelEnum.Program, () =>
        {
            RuleFor(x => x.ProgramId)
                .NotEmpty().WithMessage("ProgramId is mandatory for Program-level revisions.");
            RuleFor(x => x.ProjectId)
                .Must(id => !id.HasValue || id.Value == Guid.Empty)
                .WithMessage("ProjectId must be empty for Program-level revisions.");
        });

        When(x => x.ScopeLevel == TaskLevelEnum.Project, () =>
        {
            RuleFor(x => x.ProgramId)
                .NotEmpty().WithMessage("ProgramId is mandatory for Project-level revisions.");
            RuleFor(x => x.ProjectId)
                .NotEmpty().WithMessage("ProjectId is mandatory for Project-level revisions.");
        });

        // Lineage Sentinel Validation
        When(x => x.ParentRevisionHeaderId == Guid.Empty, () =>
        {
            RuleFor(x => x.ParentVerNum)
                .Equal(-1).WithMessage("Root revisions (no parent) must have ParentVerNum = -1.");
        });

        When(x => x.ParentRevisionHeaderId != Guid.Empty, () =>
        {
            RuleFor(x => x.ParentVerNum)
                .GreaterThanOrEqualTo(1).WithMessage("Cloned revisions must have ParentVerNum >= 1.")
                .LessThan(x => x.VerNum).WithMessage("ParentVerNum must be strictly less than current VerNum.");
        });
    }
}
