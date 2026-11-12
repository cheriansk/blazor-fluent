using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Tasks;
using BlazorEnterprise.Core.Dtos.Tasks;
using BlazorEnterprise.Persistence.Context;
using BlazorEnterprise.Persistence.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace BlazorEnterprise.Persistence.Tests.Services;

public class ActionItemServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly Mock<IDateTimeProvider> _mockTime;
    private readonly Mock<ITenantContext> _mockTenant;
    private readonly Mock<ICurrentUser> _mockUser;
    private readonly Mock<IDbContextFactory<AppDbContext>> _dbFactory;

    public ActionItemServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _mockTime = new Mock<IDateTimeProvider>();
        _mockTime.Setup(t => t.UtcNow).Returns(DateTime.UtcNow);

        _mockTenant = new Mock<ITenantContext>();
        _mockTenant.Setup(t => t.HasActiveTenant).Returns(true);
        _mockTenant.Setup(t => t.TenantId).Returns(Guid.NewGuid());

        _mockUser = new Mock<ICurrentUser>();
        _mockUser.Setup(u => u.Email).Returns("test@company.local");

        _dbFactory = new Mock<IDbContextFactory<AppDbContext>>();
        _dbFactory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new AppDbContext(_options, _mockTime.Object, _mockTenant.Object, _mockUser.Object));

        using var initContext = new AppDbContext(_options, _mockTime.Object, _mockTenant.Object, _mockUser.Object);
        initContext.Database.EnsureCreated();
        initContext.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF;");
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }

    [Fact]
    public async Task CreateActionItemAsync_DirectTaskAttachment_Succeeds()
    {
        var service = new ActionItemService(_dbFactory.Object);
        var tenantId = _mockTenant.Object.TenantId;

        await using (var db = await _dbFactory.Object.CreateDbContextAsync())
        {
            var task = new ActivityTaskEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Title = "Activity: Code review and unit test additions for PR #42",
                Priority = Shared.DataListTypes.TaskPriorityEnum.Medium,
                Status = Shared.DataListTypes.TaskStatusEnum.Open,
                StartDate = DateTime.UtcNow,
                DueDate = DateTime.UtcNow.AddDays(2),
                Level = Shared.DataListTypes.TaskLevelEnum.Project
            };
            db.ActivityTasks.Add(task);
            await db.SaveChangesAsync();

            var dto = new CreateActionItemDto
            {
                Title = "Check edge case in auth token expiry",
                Status = ActionItemStatusEnum.Open,
                Complexity = 2,
                EstimatedHours = 1.5m
            };

            var created = await service.CreateActionItemAsync(task.Id, dto);

            Assert.NotNull(created);
            Assert.Equal("Check edge case in auth token expiry", created.Title);
            Assert.Equal(task.Id, created.ParentTaskId);
            Assert.Null(created.ParentSubTaskId);
            Assert.Equal(0, created.OrderIndex);
        }
    }

    [Fact]
    public async Task RollupCalculations_ComputeCorrectMetrics()
    {
        var service = new ActionItemService(_dbFactory.Object);
        var taskId = Guid.NewGuid();
        var tenantId = _mockTenant.Object.TenantId;

        await using (var db = await _dbFactory.Object.CreateDbContextAsync())
        {
            var task = new ActivityTaskEntity
            {
                Id = taskId,
                TenantId = tenantId,
                Title = "Code Review Checklist",
                Priority = Shared.DataListTypes.TaskPriorityEnum.High,
                Status = Shared.DataListTypes.TaskStatusEnum.Open,
                StartDate = DateTime.UtcNow,
                DueDate = DateTime.UtcNow.AddDays(1),
                Level = Shared.DataListTypes.TaskLevelEnum.Project
            };
            db.ActivityTasks.Add(task);

            db.ActionItems.AddRange(
                new ActionItemEntity
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ParentTaskId = taskId,
                    Title = "Item 1",
                    Status = ActionItemStatusEnum.Completed,
                    Complexity = 3
                },
                new ActionItemEntity
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ParentTaskId = taskId,
                    Title = "Item 2",
                    Status = ActionItemStatusEnum.Open,
                    Complexity = 2,
                    IsBlocked = true,
                    BlockingReason = "Waiting for credentials"
                }
            );
            await db.SaveChangesAsync();

            var count = await service.GetActionItemCountAsync(taskId);
            var percent = await service.GetCompletionPercentageAsync(taskId);
            var blocked = await service.GetBlockedCountAsync(taskId);
            var complexity = await service.GetComplexityTotalAsync(taskId);

            Assert.Equal(2, count);
            Assert.Equal(50.00m, percent);
            Assert.Equal(1, blocked);
            Assert.Equal(5, complexity);
        }
    }
}
