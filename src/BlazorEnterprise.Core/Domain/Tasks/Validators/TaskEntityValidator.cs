using FluentValidation;

namespace BlazorEnterprise.Core.Domain.Tasks.Validators;

public abstract class TaskEntityValidator<T> : BaseTaskEntityValidator<T> where T : TaskEntity
{
    protected TaskEntityValidator()
    {
    }
}

public class TaskEntityValidator : TaskEntityValidator<TaskEntity>
{
    public TaskEntityValidator()
    {
    }
}

public class SprintTaskEntityValidator : TaskEntityValidator<SprintTaskEntity>
{
    public SprintTaskEntityValidator()
    {
        RuleFor(x => x.SprintId)
            .NotEmpty().WithMessage("SprintId is mandatory for sprint tasks.");
    }
}

public class WorkflowTaskEntityValidator : TaskEntityValidator<WorkflowTaskEntity>
{
    public WorkflowTaskEntityValidator()
    {
        RuleFor(x => x.WorkflowId)
            .NotEmpty().WithMessage("WorkflowId is mandatory for workflow tasks.");

        RuleFor(x => x.WorkflowStepOrder)
            .GreaterThan(0).WithMessage("WorkflowStepOrder must be a positive integer greater than zero.");
    }
}

public class MiscTaskEntityValidator : TaskEntityValidator<MiscTaskEntity>
{
    public MiscTaskEntityValidator()
    {
    }
}
