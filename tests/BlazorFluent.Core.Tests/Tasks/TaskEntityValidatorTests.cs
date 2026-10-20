using BlazorFluent.Core.Domain.Tasks;
using BlazorFluent.Core.Domain.Tasks.Validators;
using BlazorFluent.Shared.DataListTypes;
using FluentAssertions;
using Xunit;

namespace BlazorFluent.Core.Tests.Tasks;

public class TaskEntityValidatorTests
{
    private readonly TaskEntityValidator _validator = new();

    private static TaskEntity CreateBaseTask() => new()
    {
        Id = Guid.CreateVersion7(),
        TenantId = "tenant_test",
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

    [Fact]
    public void Validate_StandaloneTask_WithoutWorkflow_Passes()
    {
        var task = CreateBaseTask();
        task.WorkflowId = null;
        task.WorkflowStepOrder = null;

        var result = _validator.Validate(task);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WorkflowTask_WithValidOrder_Passes()
    {
        var task = CreateBaseTask();
        task.WorkflowId = Guid.CreateVersion7();
        task.WorkflowStepOrder = 1;

        var result = _validator.Validate(task);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WorkflowTask_WithNullOrder_Fails()
    {
        var task = CreateBaseTask();
        task.WorkflowId = Guid.CreateVersion7();
        task.WorkflowStepOrder = null;

        var result = _validator.Validate(task);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(TaskEntity.WorkflowStepOrder) &&
                                            e.ErrorMessage.Contains("mandatory when the task belongs to a workflow"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-5)]
    public void Validate_WorkflowTask_WithZeroOrNegativeOrder_Fails(int invalidOrder)
    {
        var task = CreateBaseTask();
        task.WorkflowId = Guid.CreateVersion7();
        task.WorkflowStepOrder = invalidOrder;

        var result = _validator.Validate(task);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(TaskEntity.WorkflowStepOrder) &&
                                            e.ErrorMessage.Contains("greater than zero"));
    }

    [Fact]
    public void Validate_StandaloneTask_WithPopulatedOrder_Fails()
    {
        var task = CreateBaseTask();
        task.WorkflowId = null;
        task.WorkflowStepOrder = 3;

        var result = _validator.Validate(task);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(TaskEntity.WorkflowStepOrder) &&
                                            e.ErrorMessage.Contains("must be null when the task does not belong to a workflow"));
    }

    [Fact]
    public void Validate_EmptyGuidWorkflowId_WithPopulatedOrder_Fails()
    {
        var task = CreateBaseTask();
        task.WorkflowId = Guid.Empty;
        task.WorkflowStepOrder = 2;

        var result = _validator.Validate(task);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(TaskEntity.WorkflowStepOrder) &&
                                            e.ErrorMessage.Contains("must be null when the task does not belong to a workflow"));
    }

    // ─── BASE TASK ENTITY LEVEL SCOPING INVARIANTS ──────────────────────────

    [Fact]
    public void Validate_TenantLevel_ValidScoping_Passes()
    {
        var task = CreateBaseTask();
        task.Level = TaskLevelEnum.Tenant;
        task.ProgramId = null;
        task.ProjectId = Guid.Empty;
        task.UserId = null;

        var result = _validator.Validate(task);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_TenantLevel_WithProgramOrProject_Fails()
    {
        var task = CreateBaseTask();
        task.Level = TaskLevelEnum.Tenant;
        task.ProgramId = Guid.CreateVersion7();
        task.ProjectId = Guid.CreateVersion7();

        var result = _validator.Validate(task);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(TaskEntity.ProgramId) &&
                                            e.ErrorMessage.Contains("Tenant-level items cannot belong to a specific Program"));
        result.Errors.Should().Contain(e => e.PropertyName == nameof(TaskEntity.ProjectId) &&
                                            e.ErrorMessage.Contains("Tenant-level items cannot belong to a specific Project"));
    }

    [Fact]
    public void Validate_ProgramLevel_ValidScoping_Passes()
    {
        var task = CreateBaseTask();
        task.Level = TaskLevelEnum.Program;
        task.ProgramId = Guid.CreateVersion7();
        task.ProjectId = Guid.Empty;
        task.UserId = null;

        var result = _validator.Validate(task);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ProgramLevel_WithoutProgramId_Fails()
    {
        var task = CreateBaseTask();
        task.Level = TaskLevelEnum.Program;
        task.ProgramId = null;
        task.ProjectId = Guid.Empty;

        var result = _validator.Validate(task);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(TaskEntity.ProgramId) &&
                                            e.ErrorMessage.Contains("ProgramId is mandatory"));
    }

    [Fact]
    public void Validate_ProgramLevel_WithProjectId_Fails()
    {
        var task = CreateBaseTask();
        task.Level = TaskLevelEnum.Program;
        task.ProgramId = Guid.CreateVersion7();
        task.ProjectId = Guid.CreateVersion7();

        var result = _validator.Validate(task);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(TaskEntity.ProjectId) &&
                                            e.ErrorMessage.Contains("Program-level items cannot belong to a specific Project"));
    }

    [Fact]
    public void Validate_ProjectLevel_WithoutProjectId_Fails()
    {
        var task = CreateBaseTask();
        task.Level = TaskLevelEnum.Project;
        task.ProjectId = Guid.Empty;
        task.ProgramId = null;

        var result = _validator.Validate(task);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(TaskEntity.ProjectId) &&
                                            e.ErrorMessage.Contains("ProjectId is mandatory"));
    }

    [Fact]
    public void Validate_ProjectLevel_WithProgramId_Fails()
    {
        var task = CreateBaseTask();
        task.Level = TaskLevelEnum.Project;
        task.ProjectId = Guid.CreateVersion7();
        task.ProgramId = Guid.CreateVersion7();

        var result = _validator.Validate(task);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(TaskEntity.ProgramId) &&
                                            e.ErrorMessage.Contains("Project-level items cannot specify a direct ProgramId"));
    }

    [Fact]
    public void Validate_UserLevel_ValidScoping_Passes()
    {
        var task = CreateBaseTask();
        task.Level = TaskLevelEnum.User;
        task.UserId = "user-12345";
        task.ProgramId = null;
        task.ProjectId = Guid.Empty;

        var result = _validator.Validate(task);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_UserLevel_WithoutUserId_Fails()
    {
        var task = CreateBaseTask();
        task.Level = TaskLevelEnum.User;
        task.UserId = null;
        task.ProgramId = null;
        task.ProjectId = Guid.Empty;

        var result = _validator.Validate(task);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(TaskEntity.UserId) &&
                                            e.ErrorMessage.Contains("UserId is mandatory"));
    }

    [Fact]
    public void Validate_UserLevel_WithProgramOrProject_Fails()
    {
        var task = CreateBaseTask();
        task.Level = TaskLevelEnum.User;
        task.UserId = "user-12345";
        task.ProgramId = Guid.CreateVersion7();
        task.ProjectId = Guid.CreateVersion7();

        var result = _validator.Validate(task);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(TaskEntity.ProgramId) &&
                                            e.ErrorMessage.Contains("User-level items cannot belong to a Program"));
        result.Errors.Should().Contain(e => e.PropertyName == nameof(TaskEntity.ProjectId) &&
                                            e.ErrorMessage.Contains("User-level items cannot belong to a Project"));
    }
}
