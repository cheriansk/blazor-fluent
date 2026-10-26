using System;
using System.Threading.Tasks;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.Domain.Tasks;
using BlazorFluent.Persistence.Context;
using BlazorFluent.Persistence.Interceptors;
using BlazorFluent.Shared.DataListTypes;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace BlazorFluent.Persistence.Tests.Interceptors;

public class ProjectSecurityInterceptorTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();

    public ProjectSecurityInterceptorTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        _connection.Open();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private TaskEntity CreateTestTask(string title) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = _tenantId,
        ProjectId = _projectId,
        Title = title,
        Level = TaskLevelEnum.Project,
        Priority = TaskPriorityEnum.Medium,
        Status = TaskStatusEnum.Open,
        StartDate = DateTime.UtcNow,
        DueDate = DateTime.UtcNow.AddDays(7)
    };

    private (AppDbContext Context, Mock<IHierarchyAuthorizationService> MockHierarchyAuth) CreateContext(
        string userId,
        bool isAuthenticated = true,
        bool isRootAdmin = false,
        bool isSystemDaemon = false)
    {
        var mockHierarchyAuth = new Mock<IHierarchyAuthorizationService>();

        var services = new ServiceCollection();
        services.AddScoped(_ => mockHierarchyAuth.Object);
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var interceptor = new ProjectSecurityInterceptor(scopeFactory, NullLogger<ProjectSecurityInterceptor>.Instance);

        var mockTime = new Mock<IDateTimeProvider>();
        mockTime.Setup(t => t.UtcNow).Returns(DateTime.UtcNow);

        var mockTenant = new Mock<ITenantContext>();
        mockTenant.Setup(t => t.TenantId).Returns(_tenantId);
        mockTenant.Setup(t => t.HasActiveTenant).Returns(true);

        var mockUser = new Mock<ICurrentUser>();
        mockUser.Setup(u => u.UserId).Returns(userId);
        mockUser.Setup(u => u.IsAuthenticated).Returns(isAuthenticated);
        mockUser.Setup(u => u.IsRootAdmin).Returns(isRootAdmin);
        mockUser.Setup(u => u.IsSystemDaemon).Returns(isSystemDaemon);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(interceptor)
            .Options;

        var context = new AppDbContext(options, mockTime.Object, mockTenant.Object, mockUser.Object);
        context.Database.EnsureCreated();

        return (context, mockHierarchyAuth);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenUserHasNoProjectRole_ThrowsUnauthorizedAccessException()
    {
        var (context, mockHierarchyAuth) = CreateContext("user_alice");
        mockHierarchyAuth
            .Setup(h => h.GetEffectiveProjectRoleAsync(_projectId, "user_alice", default))
            .ReturnsAsync((ProjectRoleEnum?)null);

        var task = CreateTestTask("Restricted Task");
        context.Tasks.Add(task);

        var act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*does not have write permissions for project*");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenUserHasReadOnlyRole_ThrowsUnauthorizedAccessException()
    {
        var (context, mockHierarchyAuth) = CreateContext("user_bob");
        mockHierarchyAuth
            .Setup(h => h.GetEffectiveProjectRoleAsync(_projectId, "user_bob", default))
            .ReturnsAsync(ProjectRoleEnum.ReadOnly);

        var task = CreateTestTask("Read Only Task");
        context.Tasks.Add(task);

        var act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*does not have write permissions for project*");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenUserHasContributeRole_Succeeds()
    {
        var (context, mockHierarchyAuth) = CreateContext("user_carol");
        mockHierarchyAuth
            .Setup(h => h.GetEffectiveProjectRoleAsync(_projectId, "user_carol", default))
            .ReturnsAsync(ProjectRoleEnum.Contribute);

        var task = CreateTestTask("Contributor Task");
        context.Tasks.Add(task);

        var result = await context.SaveChangesAsync();
        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenRootAdmin_BypassesProjectRBACAndSucceeds()
    {
        var (context, _) = CreateContext("admin_root", isRootAdmin: true);

        var task = CreateTestTask("Root Admin Task");
        context.Tasks.Add(task);

        var result = await context.SaveChangesAsync();
        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenUnauthenticated_ThrowsUnauthorizedAccessException()
    {
        var (context, _) = CreateContext("anonymous", isAuthenticated: false);

        var task = CreateTestTask("Anonymous Task");
        context.Tasks.Add(task);

        var act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Unauthenticated write to project-scoped entity is forbidden*");
    }
}
