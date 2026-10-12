using FluentValidation;

namespace BlazorFluent.Core.Domain.Tasks;

/// <summary>
/// Tier 2 Entity Invariant Validator for TaskDependencyEntity.
/// Validates in-memory dependency constraints before database persistence.
/// Executed automatically by EntityValidationInterceptor during SaveChangesAsync.
/// </summary>
public class TaskDependencyEntityValidator : AbstractValidator<TaskDependencyEntity>
{
    public TaskDependencyEntityValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage("ProjectId is mandatory for task dependencies.");

        RuleFor(x => x.TaskId)
            .NotEmpty().WithMessage("TaskId is mandatory.");

        RuleFor(x => x.DependsOnTaskId)
            .NotEmpty().WithMessage("DependsOnTaskId is mandatory.");

        RuleFor(x => x)
            .Must(x => x.TaskId != x.DependsOnTaskId)
            .WithMessage("A task cannot depend on or block itself.");

        RuleFor(x => x.DependencyType)
            .IsInEnum().WithMessage("A valid task dependency type must be specified.");

        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("Dependency notes cannot exceed 1,000 characters.");
    }
}
