using BlazorFluent.Core.Domain.Tasks;
using BlazorFluent.Core.Domain.Tasks.Validators;
using BlazorFluent.Shared.DataListTypes;
using FluentAssertions;
using Xunit;

namespace BlazorFluent.Core.Tests.Tasks;

public class WorkDependencyEntityValidatorTests
{
    private readonly WorkDependencyEntityValidator _validator = new();

    [Fact]
    public void Validate_ValidWorkDependency_Passes()
    {
        var dependency = new WorkDependencyEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = Guid.CreateVersion7(),
            Level = TaskLevelEnum.Project,
            DependentType = DependencyNodeTypeEnum.TaskItem,
            DependentItemId = Guid.CreateVersion7(),
            DependsOnType = DependencyNodeTypeEnum.TaskItem,
            DependsOnItemId = Guid.CreateVersion7(),
            Title = "Backend API Blocker",
            Description = "Waiting on backend API endpoints to deploy before UI integration can proceed.",
            Priority = TaskPriorityEnum.High,
            Criticality = DependencyCriticalityEnum.High,
            Status = TaskStatusEnum.Open,
            StartDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(3)
        };

        var result = _validator.Validate(dependency);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        dependency.Priority.Should().Be(TaskPriorityEnum.High);
        dependency.ImpactDetails.Should().Be(dependency.Description);
        dependency.ResolveDueDate.Should().Be(dependency.DueDate);
    }

    [Fact]
    public void Validate_SelfDependency_Fails()
    {
        var sameItemId = Guid.CreateVersion7();
        var dependency = new WorkDependencyEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = Guid.CreateVersion7(),
            Level = TaskLevelEnum.Project,
            DependentType = DependencyNodeTypeEnum.TaskItem,
            DependentItemId = sameItemId,
            DependsOnType = DependencyNodeTypeEnum.TaskItem,
            DependsOnItemId = sameItemId, // Cannot depend on self
            Title = "Self blocker",
            Description = "Impact description",
            Priority = TaskPriorityEnum.Urgent,
            Criticality = DependencyCriticalityEnum.Critical,
            Status = TaskStatusEnum.Open,
            StartDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(1)
        };

        var result = _validator.Validate(dependency);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("An item cannot depend on itself"));
        dependency.Priority.Should().Be(TaskPriorityEnum.Urgent); // Critical criticality maps to Urgent priority
    }

    [Fact]
    public void Validate_MissingTitle_Fails()
    {
        var dependency = new WorkDependencyEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = Guid.CreateVersion7(),
            Level = TaskLevelEnum.Project,
            DependentType = DependencyNodeTypeEnum.TaskItem,
            DependentItemId = Guid.CreateVersion7(),
            DependsOnType = DependencyNodeTypeEnum.TaskItem,
            DependsOnItemId = Guid.CreateVersion7(),
            Title = "", // Missing
            Description = "Some details",
            Priority = TaskPriorityEnum.Medium,
            Criticality = DependencyCriticalityEnum.Medium,
            Status = TaskStatusEnum.Open,
            StartDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(5)
        };

        var result = _validator.Validate(dependency);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(WorkDependencyEntity.Title));
    }
}
