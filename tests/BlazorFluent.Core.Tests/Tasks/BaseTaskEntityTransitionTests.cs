using System;
using BlazorFluent.Core.Domain.Tasks;
using BlazorFluent.Shared.DataListTypes;
using FluentAssertions;
using Xunit;

namespace BlazorFluent.Core.Tests.Tasks;

public class BaseTaskEntityTransitionTests
{
    private class TestTaskEntity : BaseTaskEntity
    {
        public override string GetDefaultCategory() => TaskStatusEnumDefinitions.Categories.Task;
    }

    [Fact]
    public void TransitionTo_InProgress_SetsActualStartDate()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var task = new TestTaskEntity
        {
            Id = Guid.CreateVersion7(),
            Title = "Task A",
            Level = TaskLevelEnum.Project,
            Priority = TaskPriorityEnum.Medium,
            StartDate = now,
            DueDate = now.AddDays(7),
            Status = TaskStatusEnum.Open
        };

        // Act
        task.TransitionTo(TaskStatusEnum.InProgress, now);

        // Assert
        task.Status.Should().Be(TaskStatusEnum.InProgress);
        task.ActualStartDate.Should().Be(now);
        task.IsClosed.Should().BeFalse();
        task.ClosedAtUtc.Should().BeNull();
    }

    [Fact]
    public void TransitionTo_Closed_SetsActualEndDate_AndComputesIsClosedAndClosedAtUtc()
    {
        // Arrange
        var startTime = DateTime.UtcNow.AddHours(-2);
        var closeTime = DateTime.UtcNow;
        var task = new TestTaskEntity
        {
            Id = Guid.CreateVersion7(),
            Title = "Task B",
            Level = TaskLevelEnum.Project,
            Priority = TaskPriorityEnum.Medium,
            StartDate = startTime,
            DueDate = startTime.AddDays(7),
            Status = TaskStatusEnum.InProgress,
            ActualStartDate = startTime
        };

        // Act
        task.TransitionTo(TaskStatusEnum.Closed, closeTime);

        // Assert
        task.Status.Should().Be(TaskStatusEnum.Closed);
        task.ActualEndDate.Should().Be(closeTime);
        task.IsClosed.Should().BeTrue();
        task.ClosedAtUtc.Should().Be(closeTime);
    }

    [Fact]
    public void TransitionTo_ReopenFromClosed_ResetsActualEndDate_AndIsClosed()
    {
        // Arrange
        var startTime = DateTime.UtcNow.AddHours(-4);
        var closeTime = DateTime.UtcNow.AddHours(-1);
        var reopenTime = DateTime.UtcNow;
        var task = new TestTaskEntity
        {
            Id = Guid.CreateVersion7(),
            Title = "Task C",
            Level = TaskLevelEnum.Project,
            Priority = TaskPriorityEnum.Medium,
            StartDate = startTime,
            DueDate = startTime.AddDays(7),
            Status = TaskStatusEnum.Closed,
            ActualStartDate = startTime,
            ActualEndDate = closeTime
        };

        task.IsClosed.Should().BeTrue();
        task.ClosedAtUtc.Should().Be(closeTime);

        // Act
        task.TransitionTo(TaskStatusEnum.InProgress, reopenTime);

        // Assert
        task.Status.Should().Be(TaskStatusEnum.InProgress);
        task.ActualEndDate.Should().BeNull();
        task.IsClosed.Should().BeFalse();
        task.ClosedAtUtc.Should().BeNull();
    }
}
