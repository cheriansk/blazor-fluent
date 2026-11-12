using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Tasks;
using BlazorEnterprise.Core.Domain.Tenancy;
using BlazorEnterprise.Core.Dtos.Response;
using BlazorEnterprise.Core.Dtos.Tasks;
using BlazorEnterprise.Persistence.Context;
using BlazorEnterprise.Persistence.Services;
using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace BlazorEnterprise.Persistence.Tests.Services;

public class TaskCreationServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
    private readonly Mock<ITenantContext> _tenantContext;
    private readonly Mock<ICurrentUser> _currentUser;
    private readonly Mock<IProjectAuthorizationService> _projectAuth;
    private readonly Mock<IUnitOfWork> _unitOfWork;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();

    public TaskCreationServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        _connection.Open();

        _dateTimeProvider = new Mock<IDateTimeProvider>();
        _dateTimeProvider.Setup(d => d.Now).Returns(DateTime.UtcNow);
        _dateTimeProvider.Setup(d => d.UtcNow).Returns(DateTime.UtcNow);

        _tenantContext = new Mock<ITenantContext>();
        _tenantContext.Setup(t => t.HasActiveTenant).Returns(true);
        _tenantContext.Setup(t => t.TenantId).Returns(_tenantId);

        _currentUser = new Mock<ICurrentUser>();
        _currentUser.Setup(c => c.UserId).Returns("user-123");
        _currentUser.Setup(c => c.Email).Returns("user@example.com");

        _projectAuth = new Mock<IProjectAuthorizationService>();
        _projectAuth.Setup(p => p.EnsureCanEditAsync(It.IsAny<Guid>(), It.IsAny<ProjectRoleEnum>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWork = new Mock<IUnitOfWork>();

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var initContext = new AppDbContext(_options, _dateTimeProvider.Object, _tenantContext.Object, _currentUser.Object);
        initContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public void CalculateRiskExposure_ReturnsCorrectProduct()
    {
        var dbFactory = new Mock<IDbContextFactory<AppDbContext>>();
        var service = new TaskCreationService(
            dbFactory.Object,
            _unitOfWork.Object,
            _projectAuth.Object,
            _tenantContext.Object,
            _currentUser.Object,
            NullLogger<TaskCreationService>.Instance);

        var exposure = service.CalculateRiskExposure(RiskProbabilityEnum.High, RiskImpactEnum.Critical);
        Assert.Equal(12, exposure);

        var lowExposure = service.CalculateRiskExposure(RiskProbabilityEnum.Low, RiskImpactEnum.Low);
        Assert.Equal(1, lowExposure);
    }

    [Fact]
    public async Task ValidateTaskDataAsync_WhenTitleMissing_ReturnsFailure()
    {
        var dbFactory = new Mock<IDbContextFactory<AppDbContext>>();
        var service = new TaskCreationService(
            dbFactory.Object,
            _unitOfWork.Object,
            _projectAuth.Object,
            _tenantContext.Object,
            _currentUser.Object,
            NullLogger<TaskCreationService>.Instance);

        var request = new TaskCreationRequestDto
        {
            TaskType = CreationTaskTypeEnum.ActivityTask,
            Title = ""
        };

        var result = await service.ValidateTaskDataAsync(request);
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("Title is required"));
    }

    [Fact]
    public async Task ValidateTaskDataAsync_WhenSprintTaskWithoutSprint_ReturnsFailure()
    {
        var dbFactory = new Mock<IDbContextFactory<AppDbContext>>();
        var service = new TaskCreationService(
            dbFactory.Object,
            _unitOfWork.Object,
            _projectAuth.Object,
            _tenantContext.Object,
            _currentUser.Object,
            NullLogger<TaskCreationService>.Instance);

        var request = new TaskCreationRequestDto
        {
            TaskType = CreationTaskTypeEnum.SprintTask,
            Title = "Valid Title",
            SprintId = null
        };

        var result = await service.ValidateTaskDataAsync(request);
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("Sprint must be selected"));
    }

    [Fact]
    public async Task ValidateTaskDataAsync_WhenRiskMitigationStrategyTooShort_ReturnsFailure()
    {
        var dbFactory = new Mock<IDbContextFactory<AppDbContext>>();
        var service = new TaskCreationService(
            dbFactory.Object,
            _unitOfWork.Object,
            _projectAuth.Object,
            _tenantContext.Object,
            _currentUser.Object,
            NullLogger<TaskCreationService>.Instance);

        var request = new TaskCreationRequestDto
        {
            TaskType = CreationTaskTypeEnum.Risk,
            Title = "Valid Title",
            MitigationStrategy = "short"
        };

        var result = await service.ValidateTaskDataAsync(request);
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("Mitigation strategy is required"));
    }
}
