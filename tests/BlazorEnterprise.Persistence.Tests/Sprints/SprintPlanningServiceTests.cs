using BlazorEnterprise.Core.Models;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Identity;
using BlazorEnterprise.Core.Domain.Sprints;
using BlazorEnterprise.Core.Domain.Tasks;
using BlazorEnterprise.Core.Domain.Tenancy;
using BlazorEnterprise.Persistence.Context;
using BlazorEnterprise.Persistence.Interceptors;
using BlazorEnterprise.Persistence.Services;
using BlazorEnterprise.Persistence.Services.RevisionProviders;
using BlazorEnterprise.Shared.DataListTypes;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace BlazorEnterprise.Persistence.Tests.Sprints;

public class SprintPlanningServiceTests : IDisposable
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _programId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly SqliteConnection _connection;

    public SprintPlanningServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        _connection.Open();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private (AppDbContext Context, SprintPlanningService Service) CreateHarness()
    {
        var mockTenant = new Mock<ITenantContext>();
        mockTenant.Setup(t => t.TenantId).Returns(_tenantId);
        mockTenant.Setup(t => t.HasActiveTenant).Returns(true);
        mockTenant.Setup(t => t.IsRootTenant).Returns(false);

        var mockUser = new Mock<ICurrentUser>();
        mockUser.Setup(u => u.UserId).Returns("usr_pm");
        mockUser.Setup(u => u.Email).Returns("pm@example.com");
        mockUser.Setup(u => u.IsAuthenticated).Returns(true);

        var mockTime = new Mock<IDateTimeProvider>();
        var testTime = new DateTime(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);
        mockTime.Setup(t => t.UtcNow).Returns(testTime);
        mockTime.Setup(t => t.Now).Returns(testTime);

        var interceptor = new AuditableEntityInterceptor();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(interceptor)
            .Options;

        var dbFactoryMock = new Mock<IDbContextFactory<AppDbContext>>();
        dbFactoryMock.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new AppDbContext(options, mockTime.Object, mockTenant.Object, mockUser.Object));

        var providers = new IRevisionRowProvider[]
        {
            new SprintItemRevisionRowProvider(dbFactoryMock.Object)
        };

        var revisionService = new RevisionService(
            dbFactoryMock.Object,
            mockTenant.Object,
            mockUser.Object,
            mockTime.Object,
            NullLogger<RevisionService>.Instance,
            providers);

        var service = new SprintPlanningService(
            dbFactoryMock.Object,
            revisionService,
            mockTenant.Object,
            mockUser.Object,
            mockTime.Object,
            NullLogger<SprintPlanningService>.Instance);

        var context = new AppDbContext(options, mockTime.Object, mockTenant.Object, mockUser.Object);
        context.Database.EnsureCreated();
        return (context, service);
    }

    private void SeedProject(AppDbContext context, DateTime start, DateTime end)
    {
        var project = new ProjectEntity
        {
            Id = _projectId,
            TenantId = _tenantId,
            ProgramId = _programId,
            Name = "Sprint App",
            ShortCode = "SPRT",
            StartDate = start,
            DueDate = end,
            Status = ProjectStatusEnum.Active,
            CreatedBy = "system",
            UpdatedBy = "system"
        };
        context.Projects.Add(project);
        context.SaveChanges();
    }

    [Fact]
    public async Task CreateSprintAsync_WithinProjectDates_SucceedsAndAudits()
    {
        var (context, service) = CreateHarness();
        var projStart = DateTime.UtcNow.Date.AddDays(-10);
        var projEnd = DateTime.UtcNow.Date.AddDays(60);
        SeedProject(context, projStart, projEnd);

        var sprintStart = DateTime.UtcNow.Date.AddDays(1);
        var sprintEnd = sprintStart.AddDays(14);

        var sprint = await service.CreateSprintAsync(_projectId, "Sprint 1", 1, sprintStart, sprintEnd, "First sprint");

        sprint.Should().NotBeNull();
        sprint.SprintName.Should().Be("Sprint 1");
        sprint.SprintNumber.Should().Be(1);

        var auditLogs = await service.GetSprintAuditHistoryAsync(sprint.Id);
        auditLogs.Should().HaveCount(1);
        auditLogs[0].ChangeType.Should().Be("Created");
    }

    [Fact]
    public async Task CreateSprintAsync_OutsideProjectDates_ThrowsInvalidOperationException()
    {
        var (context, service) = CreateHarness();
        var projStart = DateTime.UtcNow.Date;
        var projEnd = DateTime.UtcNow.Date.AddDays(30);
        SeedProject(context, projStart, projEnd);

        var sprintStart = projStart.AddDays(-5);
        var sprintEnd = projStart.AddDays(10);

        var act = async () => await service.CreateSprintAsync(_projectId, "Sprint 0", 0, sprintStart, sprintEnd, null);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Sprint dates must fall within the project start and due dates.*");
    }

    [Fact]
    public async Task MoveItemsToSprintAsync_CreatesNewItemVersions()
    {
        var (context, service) = CreateHarness();
        var projStart = DateTime.UtcNow.Date.AddDays(-10);
        var projEnd = DateTime.UtcNow.Date.AddDays(60);
        SeedProject(context, projStart, projEnd);

        var sprint = await service.CreateSprintAsync(_projectId, "Sprint 1", 1, projStart.AddDays(1), projStart.AddDays(14), null);

        var task = new MiscTaskEntity
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            ProjectId = _projectId,
            Level = TaskLevelEnum.Project,
            Title = "Implement auth",
            Priority = TaskPriorityEnum.High,
            Status = TaskStatusEnum.Open,
            StartDate = projStart.AddDays(1),
            DueDate = projStart.AddDays(14),
            CreatedBy = "system",
            UpdatedBy = "system"
        };
        context.Tasks.Add(task);
        await context.SaveChangesAsync();

        // Move task into Sprint 1
        await service.MoveItemsToSprintAsync(new List<Guid> { task.Id }, sprint.Id, _projectId);

        var items = await service.GetSprintItemsAsync(sprint.Id, _projectId);
        items.Should().HaveCount(1);
        items[0].ItemId.Should().Be(task.Id);
        items[0].SprintName.Should().Be("Sprint 1");

        // Move task to Backlog (null sprint)
        await service.MoveItemsToSprintAsync(new List<Guid> { task.Id }, null, _projectId);

        var sprintItemsAfter = await service.GetSprintItemsAsync(sprint.Id, _projectId);
        sprintItemsAfter.Should().BeEmpty();

        var backlogItems = await service.GetSprintItemsAsync(null, _projectId);
        backlogItems.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetSprintMetricsAsync_CalculatesRollupsAccurately()
    {
        var (context, service) = CreateHarness();
        var projStart = DateTime.UtcNow.Date.AddDays(-10);
        var projEnd = DateTime.UtcNow.Date.AddDays(60);
        SeedProject(context, projStart, projEnd);

        var sprint = await service.CreateSprintAsync(_projectId, "Sprint 1", 1, projStart.AddDays(1), projStart.AddDays(14), null);

        var task1 = new MiscTaskEntity
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            ProjectId = _projectId,
            Level = TaskLevelEnum.Project,
            Title = "Task 1 Done",
            Priority = TaskPriorityEnum.Medium,
            Status = TaskStatusEnum.Closed,
            StartDate = projStart.AddDays(1),
            DueDate = projStart.AddDays(14),
            CreatedBy = "system",
            UpdatedBy = "system"
        };
        var task2 = new MiscTaskEntity
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            ProjectId = _projectId,
            Level = TaskLevelEnum.Project,
            Title = "Task 2 InProgress",
            Priority = TaskPriorityEnum.High,
            Status = TaskStatusEnum.InProgress,
            StartDate = projStart.AddDays(1),
            DueDate = projStart.AddDays(14),
            CreatedBy = "system",
            UpdatedBy = "system"
        };
        context.Tasks.AddRange(task1, task2);
        await context.SaveChangesAsync();

        await service.MoveItemsToSprintAsync(new List<Guid> { task1.Id, task2.Id }, sprint.Id, _projectId);

        var metrics = await service.GetSprintMetricsAsync(sprint.Id, _projectId);

        metrics.TotalItemCount.Should().Be(2);
        metrics.SprintName.Should().Be("Sprint 1");
    }
}
