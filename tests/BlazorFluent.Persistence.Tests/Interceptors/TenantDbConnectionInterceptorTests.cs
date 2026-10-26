using System;
using System.Threading.Tasks;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Persistence.Context;
using BlazorFluent.Persistence.Interceptors;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace BlazorFluent.Persistence.Tests.Interceptors;

public class TenantDbConnectionInterceptorTests : IDisposable
{
    private readonly SqliteConnection _sqliteConnection;

    public TenantDbConnectionInterceptorTests()
    {
        _sqliteConnection = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
    }

    public void Dispose()
    {
        _sqliteConnection.Dispose();
    }

    private AppDbContext CreateContext(ITenantContext tenantContext)
    {
        var interceptor = new TenantDbConnectionInterceptor(tenantContext);

        var mockTime = new Mock<IDateTimeProvider>();
        mockTime.Setup(t => t.UtcNow).Returns(DateTime.UtcNow);

        var mockUser = new Mock<ICurrentUser>();
        mockUser.Setup(u => u.IsAuthenticated).Returns(true);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_sqliteConnection)
            .AddInterceptors(interceptor)
            .Options;

        return new AppDbContext(options, mockTime.Object, tenantContext, mockUser.Object);
    }

    [Fact]
    public void ConnectionOpened_OnNonPostgreSqlConnection_ExecutesWithoutError()
    {
        // Arrange
        var mockTenant = new Mock<ITenantContext>();
        mockTenant.Setup(t => t.HasActiveTenant).Returns(true);
        mockTenant.Setup(t => t.TenantId).Returns(Guid.NewGuid());

        using var context = CreateContext(mockTenant.Object);

        // Act & Assert (Opening connection triggers TenantDbConnectionInterceptor.ConnectionOpened)
        var act = () => context.Database.OpenConnection();
        act.Should().NotThrow();
    }

    [Fact]
    public async Task ConnectionOpenedAsync_OnNonPostgreSqlConnection_ExecutesWithoutError()
    {
        // Arrange
        var mockTenant = new Mock<ITenantContext>();
        mockTenant.Setup(t => t.HasActiveTenant).Returns(true);
        mockTenant.Setup(t => t.TenantId).Returns(Guid.NewGuid());

        using var context = CreateContext(mockTenant.Object);

        // Act & Assert (Opening connection triggers TenantDbConnectionInterceptor.ConnectionOpenedAsync)
        var act = () => context.Database.OpenConnectionAsync();
        await act.Should().NotThrowAsync();
    }
}
