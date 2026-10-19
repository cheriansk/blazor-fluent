using BlazorFluent.Core.Domain.Tasks;
using BlazorFluent.Core.Domain.Tasks.Validators;
using BlazorFluent.Shared.DataListTypes;
using FluentAssertions;
using Xunit;

namespace BlazorFluent.Core.Tests.Tasks;

public class BlockerDependencyEntityValidatorTests
{
    private readonly BlockerDependencyEntityValidator _validator = new();

    [Fact]
    public void Validate_ValidBlocker_Passes()
    {
        var blocker = new BlockerDependencyEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = "tenant_test",
            Level = TaskLevel.Project,
            SourceTaskId = Guid.CreateVersion7(),
            TargetTaskId = Guid.CreateVersion7(),
            Title = "Blocker Contract",
            Description = "Waiting on backend API endpoints to deploy before UI integration can proceed.",
            Criticality = DependencyCriticality.High,
            Status = TaskStatus.Open,
            StartDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(3)
        };

        var result = _validator.Validate(blocker);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        blocker.Priority.Should().Be(TaskPriority.High);
        blocker.ImpactDetails.Should().Be(blocker.Description);
        blocker.ResolveDueDate.Should().Be(blocker.DueDate);
    }

    [Fact]
    public void Validate_SelfBlocking_Fails()
    {
        var sameTaskId = Guid.CreateVersion7();
        var blocker = new BlockerDependencyEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = "tenant_test",
            Level = TaskLevel.Project,
            SourceTaskId = sameTaskId,
            TargetTaskId = sameTaskId, // Cannot block self
            Title = "Self blocker",
            Description = "Impact description",
            Criticality = DependencyCriticality.Critical,
            Status = TaskStatus.Open,
            StartDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(1)
        };

        var result = _validator.Validate(blocker);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("A task cannot block itself"));
        blocker.Priority.Should().Be(TaskPriority.Urgent); // Critical criticality maps to Urgent priority
    }

    [Fact]
    public void Validate_MissingDescription_Fails()
    {
        var blocker = new BlockerDependencyEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = "tenant_test",
            Level = TaskLevel.Project,
            SourceTaskId = Guid.CreateVersion7(),
            TargetTaskId = Guid.CreateVersion7(),
            Title = "Missing description blocker",
            Description = "", // Missing
            Criticality = DependencyCriticality.Medium,
            Status = TaskStatus.Open,
            StartDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(5)
        };

        var result = _validator.Validate(blocker);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(BlockerDependencyEntity.Description));
    }
}
