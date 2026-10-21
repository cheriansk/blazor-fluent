using System;
using BlazorFluent.Shared.DataListTypes;
using FluentValidation;

namespace BlazorFluent.Core.Domain.Tasks.Validators;

/// <summary>
/// Abstract base validator enforcing common work item invariants and strict organizational level scoping.
/// </summary>
/// <typeparam name="T">Any entity inheriting from <see cref="BaseTaskEntity"/>.</typeparam>
public abstract class BaseTaskEntityValidator<T> : AbstractValidator<T> where T : BaseTaskEntity
{
    protected BaseTaskEntityValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("TenantId is mandatory for all work items.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title cannot exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(10000).WithMessage("Description cannot exceed 10,000 characters.");

        RuleFor(x => x.DueDate)
            .NotEmpty().WithMessage("Due Date is required.")
            .GreaterThanOrEqualTo(x => x.StartDate).WithMessage("Due Date cannot precede Start Date.");

        When(x => x.EndDate.HasValue, () =>
        {
            RuleFor(x => x.EndDate!.Value)
                .GreaterThanOrEqualTo(x => x.StartDate)
                .WithMessage("End Date cannot precede Start Date.");
        });

        When(x => x.RevisedDueDate.HasValue, () =>
        {
            RuleFor(x => x.RevisedDueDate!.Value)
                .GreaterThanOrEqualTo(x => x.StartDate)
                .WithMessage("Revised Due Date cannot precede Start Date.");
        });

        RuleFor(x => x.ReporterEmail)
            .MaximumLength(256).WithMessage("Reporter email cannot exceed 256 characters.");

        RuleFor(x => x.AssigneeEmails)
            .MaximumLength(2000).WithMessage("Assignee emails string cannot exceed 2,000 characters.");

        RuleFor(x => x.WatcherEmails)
            .MaximumLength(2000).WithMessage("Watcher emails string cannot exceed 2,000 characters.");

        RuleFor(x => x.Labels)
            .MaximumLength(500).WithMessage("Labels string cannot exceed 500 characters.");

        // ─── STRICT HIERARCHY SCOPING INVARIANTS ───────────────────────────────────

        // 1. Tenant Level: executive tenant-wide governance item; strictly no ProgramId or ProjectId
        When(x => x.Level == TaskLevelEnum.Tenant, () =>
        {
            RuleFor(x => x.ProgramId)
                .Null().WithMessage("Tenant-level items cannot belong to a specific Program.");

            RuleFor(x => x.ProjectId)
                .Null().WithMessage("Tenant-level items cannot belong to a specific Project.");

            RuleFor(x => x.UserId)
                .Null().WithMessage("Tenant-level items cannot be personal user items.");
        });

        // 2. Program Level: multi-project strategic initiative; ProgramId mandatory, ProjectId strictly null
        When(x => x.Level == TaskLevelEnum.Program, () =>
        {
            RuleFor(x => x.ProgramId)
                .NotNull().WithMessage("ProgramId is mandatory for Program-level items.")
                .Must(id => id.HasValue && id.Value != Guid.Empty).WithMessage("A valid ProgramId is mandatory for Program-level items.");

            RuleFor(x => x.ProjectId)
                .Null().WithMessage("Program-level items cannot belong to a specific Project.");

            RuleFor(x => x.UserId)
                .Null().WithMessage("Program-level items cannot be personal user items.");
        });

        // 3. Project Level: team delivery item; ProjectId mandatory, ProgramId strictly null (traversed via Project.ProgramId)
        When(x => x.Level == TaskLevelEnum.Project, () =>
        {
            RuleFor(x => x.ProjectId)
                .NotNull().WithMessage("ProjectId is mandatory for Project-level items.")
                .Must(id => id.HasValue && id.Value != Guid.Empty).WithMessage("A valid ProjectId is mandatory for Project-level items.");

            RuleFor(x => x.ProgramId)
                .Null().WithMessage("Project-level items cannot specify a direct ProgramId (inherited via Project).");

            RuleFor(x => x.UserId)
                .Null().WithMessage("Project-level items cannot be personal user items.");
        });

        // 4. User Level: personal workload item; UserId mandatory, ProgramId & ProjectId strictly null
        When(x => x.Level == TaskLevelEnum.User, () =>
        {
            RuleFor(x => x.UserId)
                .NotEmpty().WithMessage("UserId is mandatory for personal User-level items.");

            RuleFor(x => x.ProgramId)
                .Null().WithMessage("User-level items cannot belong to a Program.");

            RuleFor(x => x.ProjectId)
                .Null().WithMessage("User-level items cannot belong to a Project.");
        });
    }
}
