using BlazorFluent.Core.Domain.Jobs;
using BlazorFluent.Shared.DataListTypes;
using FluentAssertions;
using Xunit;

namespace BlazorFluent.Core.Tests.Jobs;

public class JobExecutionEntityValidatorTests
{
    private readonly JobExecutionEntityValidator _validator = new();

    [Fact]
    public void Validate_ValidTenantJobExecution_Passes()
    {
        var execution = new JobExecutionEntity
        {
            Id = Guid.CreateVersion7(),
            JobName = "TaskCadenceAlertJob",
            TenantId = Guid.NewGuid(),
            CorrelationId = "corr_12345678",
            TriggerSource = JobTriggerSourceEnum.Manual,
            Status = JobStatusEnum.Running,
            AttemptCount = 1,
            MaxRetries = 3,
            StartedAt = DateTime.UtcNow
        };

        var result = _validator.Validate(execution);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_ValidHostSystemJobExecution_Passes()
    {
        var execution = new JobExecutionEntity
        {
            Id = Guid.CreateVersion7(),
            JobName = "DatabaseVacuumJob",
            TenantId = Guid.CreateVersion7(),
            CorrelationId = "corr_system_999",
            TriggerSource = JobTriggerSourceEnum.Cron,
            Status = JobStatusEnum.Succeeded,
            AttemptCount = 1,
            MaxRetries = 3,
            StartedAt = DateTime.UtcNow.AddMinutes(-5),
            CompletedAt = DateTime.UtcNow,
            DurationMs = 300000
        };

        var result = _validator.Validate(execution);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_MissingCorrelationId_Fails()
    {
        var execution = new JobExecutionEntity
        {
            Id = Guid.CreateVersion7(),
            JobName = "TaskCadenceAlertJob",
            TenantId = Guid.NewGuid(),
            CorrelationId = "", // Invalid
            TriggerSource = JobTriggerSourceEnum.Cron,
            Status = JobStatusEnum.Running,
            AttemptCount = 1,
            MaxRetries = 3,
            StartedAt = DateTime.UtcNow
        };

        var result = _validator.Validate(execution);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(JobExecutionEntity.CorrelationId));
    }

    [Fact]
    public void Validate_MissingTenantId_Fails()
    {
        var execution = new JobExecutionEntity
        {
            Id = Guid.CreateVersion7(),
            JobName = "TaskCadenceAlertJob",
            TenantId = Guid.Empty, // Invalid
            CorrelationId = "corr_12345",
            TriggerSource = JobTriggerSourceEnum.Cron,
            Status = JobStatusEnum.Running,
            AttemptCount = 1,
            MaxRetries = 3,
            StartedAt = DateTime.UtcNow
        };

        var result = _validator.Validate(execution);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(JobExecutionEntity.TenantId));
    }

    [Fact]
    public void Validate_MissingJobName_Fails()
    {
        var execution = new JobExecutionEntity
        {
            Id = Guid.CreateVersion7(),
            JobName = "", // Invalid
            TenantId = Guid.NewGuid(),
            CorrelationId = "corr_12345",
            TriggerSource = JobTriggerSourceEnum.Cron,
            Status = JobStatusEnum.Running,
            AttemptCount = 1,
            MaxRetries = 3,
            StartedAt = DateTime.UtcNow
        };

        var result = _validator.Validate(execution);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(JobExecutionEntity.JobName));
    }

    [Fact]
    public void Validate_TerminatedJobMissingCompletedAt_Fails()
    {
        var execution = new JobExecutionEntity
        {
            Id = Guid.CreateVersion7(),
            JobName = "TaskCadenceAlertJob",
            TenantId = Guid.NewGuid(),
            CorrelationId = "corr_12345",
            TriggerSource = JobTriggerSourceEnum.Cron,
            Status = JobStatusEnum.Succeeded,
            AttemptCount = 1,
            MaxRetries = 3,
            StartedAt = DateTime.UtcNow,
            CompletedAt = null // Invalid for Succeeded
        };

        var result = _validator.Validate(execution);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(JobExecutionEntity.CompletedAt));
    }
}
