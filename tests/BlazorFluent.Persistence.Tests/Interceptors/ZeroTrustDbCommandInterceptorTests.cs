using System;
using System.Threading.Tasks;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.Domain.Tasks;
using BlazorFluent.Persistence.Context;
using BlazorFluent.Persistence.Interceptors;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace BlazorFluent.Persistence.Tests.Interceptors;

public class ZeroTrustDbCommandInterceptorTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public ZeroTrustDbCommandInterceptorTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        _connection.Open();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private AppDbContext CreateContext(bool isAuthenticated, bool isSystemDaemon)
    {
        var interceptor = new ZeroTrustDbCommandInterceptor(NullLogger<ZeroTrustDbCommandInterceptor>.Instance);

        var mockTime = new Mock<IDateTimeProvider>();
        mockTime.Setup(t => t.UtcNow).Returns(DateTime.UtcNow);

        var mockTenant = new Mock<ITenantContext>();
        mockTenant.Setup(t => t.TenantId).Returns(Guid.NewGuid());
        mockTenant.Setup(t => t.HasActiveTenant).Returns(true);

        var mockUser = new Mock<ICurrentUser>();
        mockUser.Setup(u => u.IsAuthenticated).Returns(isAuthenticated);
        mockUser.Setup(u => u.IsSystemDaemon).Returns(isSystemDaemon);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(interceptor)
            .Options;

        return new AppDbContext(options, mockTime.Object, mockTenant.Object, mockUser.Object);
    }

    [Fact]
    public async Task Query_WhenAuthenticated_ExecutesSuccessfully()
    {
        // Arrange
        using var context = CreateContext(isAuthenticated: true, isSystemDaemon: false);
        context.Database.EnsureCreated();

        // Act
        var act = () => context.Tasks.ToListAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Query_WhenSystemDaemon_ExecutesSuccessfully()
    {
        // Arrange
        using var context = CreateContext(isAuthenticated: false, isSystemDaemon: true);
        context.Database.EnsureCreated();

        // Act
        var act = () => context.Tasks.ToListAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Query_WhenUnauthenticated_ThrowsZeroTrustUnauthorizedAccessException()
    {
        // Arrange
        using var authContext = CreateContext(isAuthenticated: true, isSystemDaemon: false);
        authContext.Database.EnsureCreated();

        using var unauthContext = CreateContext(isAuthenticated: false, isSystemDaemon: false);

        // Act
        var act = () => unauthContext.Tasks.ToListAsync();

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Zero-Trust Security Violation: Unauthenticated contexts are completely denied*");
    }

    [Fact]
    public async Task Query_WhenUnauthenticated_OnWhitelistedAuditRecords_Allowed()
    {
        // Arrange
        using var authContext = CreateContext(isAuthenticated: true, isSystemDaemon: false);
        authContext.Database.EnsureCreated();

        using var unauthContext = CreateContext(isAuthenticated: false, isSystemDaemon: false);

        // Act
        var act = () => unauthContext.AuditRecords.ToListAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }
}
