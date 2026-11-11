using BlazorEnterprise.Core.Domain.Tasks;
using BlazorEnterprise.Core.Domain.Tasks.Validators;
using BlazorEnterprise.Shared.DataListTypes;
using FluentAssertions;
using Xunit;

namespace BlazorEnterprise.Core.Tests.Tasks;

public class TaskEntityValidatorTests
{
    private readonly MiscTaskEntityValidator _miscValidator = new();
    private readonly WorkflowTaskEntityValidator _workflowValidator = new();
    private readonly SprintTaskEntityValidator _sprintValidator = new();

    private static MiscTaskEntity CreateMiscTask() => new()
    {
        Id = Guid.CreateVersion7(),
        TenantId = Guid.CreateVersion7(),
        Level = TaskLevelEnum.Project,
        ProjectId = Guid.CreateVersion7(),
        Title = "Implement Security Gate",
        Description = "Enforce clearance policy on API endpoints",
        Priority = TaskPriorityEnum.High,
        Status = TaskStatusEnum.Open,
        StartDate = DateTime.UtcNow,
        DueDate = DateTime.UtcNow.AddDays(7),
        ReporterEmail = "lead@company.local",
        AssigneeEmails = "dev@company.local"
    };

    private static WorkflowTaskEntity CreateWorkflowTask() => new()
    {
        Id = Guid.CreateVersion7(),
        TenantId = Guid.CreateVersion7(),
        Level = TaskLevelEnum.Project,
        ProjectId = Guid.CreateVersion7(),
        WorkflowId = Guid.CreateVersion7(),
        WorkflowStepOrder = 1,
        Title = "Workflow Step Task",
        Description = "Execute pipeline step",
        Priority = TaskPriorityEnum.High,
        Status = TaskStatusEnum.Open,
        StartDate = DateTime.UtcNow,
        DueDate = DateTime.UtcNow.AddDays(7),
        ReporterEmail = "lead@company.local",
        AssigneeEmails = "dev@company.local"
    };

    private static SprintTaskEntity CreateSprintTask() => new()
    {
        Id = Guid.CreateVersion7(),
        TenantId = Guid.CreateVersion7(),
        Level = TaskLevelEnum.Project,
        ProjectId = Guid.CreateVersion7(),
        SprintId = Guid.CreateVersion7(),
        Title = "Sprint Delivery Task",
        Description = "Deliver story point item",
        Priority = TaskPriorityEnum.Medium,
        Status = TaskStatusEnum.Open,
        StartDate = DateTime.UtcNow,
        DueDate = DateTime.UtcNow.AddDays(7),
        ReporterEmail = "lead@company.local",
        AssigneeEmails = "dev@company.local"
    };

    [Fact]
    public void Validate_MiscTask_Passes()
    {
        var task = CreateMiscTask();
        var result = _miscValidator.Validate(task);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WorkflowTask_WithValidOrder_Passes()
    {
        var task = CreateWorkflowTask();
        var result = _workflowValidator.Validate(task);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-5)]
    public void Validate_WorkflowTask_WithZeroOrNegativeOrder_Fails(int invalidOrder)
    {
        var task = CreateWorkflowTask();
        task.WorkflowStepOrder = invalidOrder;

        var result = _workflowValidator.Validate(task);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(WorkflowTaskEntity.WorkflowStepOrder) &&
                                            e.ErrorMessage.Contains("greater than zero"));
    }

    [Fact]
    public void Validate_WorkflowTask_WithEmptyWorkflowId_Fails()
    {
        var task = CreateWorkflowTask();
        task.WorkflowId = Guid.Empty;

        var result = _workflowValidator.Validate(task);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(WorkflowTaskEntity.WorkflowId) &&
                                            e.ErrorMessage.Contains("mandatory for workflow tasks"));
    }

    [Fact]
    public void Validate_SprintTask_WithValidSprintId_Passes()
    {
        var task = CreateSprintTask();
        var result = _sprintValidator.Validate(task);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_SprintTask_WithEmptySprintId_Fails()
    {
        var task = CreateSprintTask();
        task.SprintId = Guid.Empty;

        var result = _sprintValidator.Validate(task);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(SprintTaskEntity.SprintId) &&
                                            e.ErrorMessage.Contains("mandatory for sprint tasks"));
    }
}
