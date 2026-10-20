using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.Domain.Tasks;
using BlazorFluent.Persistence.Context;
using BlazorFluent.Persistence.Interceptors;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace BlazorFluent.Persistence.Tests.Interceptors;

public class NoTrackingMutationGuardInterceptorTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public NoTrackingMutationGuardInterceptorTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public async Task SaveChanges_UntrackedDetachedMutation_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockTime = new Mock<IDateTimeProvider>();
        mockTime.Setup(t => t.UtcNow).Returns(DateTime.UtcNow);
        var mockTenant = new Mock<ITenantContext>();
        mockTenant.Setup(t => t.TenantId).Returns("tenant_x");
        mockTenant.Setup(t => t.IsHost).Returns(true);
        var mockUser = new Mock<ICurrentUser>();
        mockUser.Setup(u => u.IsAuthenticated).Returns(true);

        var guardInterceptor = new NoTrackingMutationGuardInterceptor();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .AddInterceptors(guardInterceptor)
            .Options;

        var taskId = Guid.NewGuid();
        using (var setup = new AppDbContext(options, mockTime.Object, mockTenant.Object, mockUser.Object))
        {
            setup.Database.EnsureCreated();
            setup.Tasks.Add(new TaskEntity
            {
                Id = taskId,
                TenantId = "tenant_x",
                Level = TaskLevelEnum.Project,
                Title = "Original Title",
                Priority = TaskPriorityEnum.Medium,
                Status = TaskStatusEnum.Open,
                StartDate = DateTime.UtcNow,
                DueDate = DateTime.UtcNow.AddDays(7)
            });
            await setup.SaveChangesAsync();
        }

        // Act & Assert
        using (var context = new AppDbContext(options, mockTime.Object, mockTenant.Object, mockUser.Object))
        {
            // Materialize without tracking
            var task = await context.Tasks.FirstAsync(t => t.Id == taskId);
            task.Title = "Mutated Untracked Title"; // Changed in memory while detached

            // Attempting to SaveChanges without tracking must trigger NoTrackingMutationGuardInterceptor
            var act = async () => await context.SaveChangesAsync();
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*NoTrackingMutationGuard*");
        }
    }
}
