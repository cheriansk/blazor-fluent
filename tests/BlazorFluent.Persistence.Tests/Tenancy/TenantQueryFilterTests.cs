using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.Domain.Tasks;
using BlazorFluent.Core.Domain.Tenancy;
using BlazorFluent.Persistence.Context;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace BlazorFluent.Persistence.Tests.Tenancy;

public class TenantQueryFilterTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public TenantQueryFilterTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private AppDbContext CreateContext(string tenantId, bool isHost = false)
    {
        var mockTime = new Mock<IDateTimeProvider>();
        mockTime.Setup(t => t.UtcNow).Returns(DateTime.UtcNow);

        var mockTenant = new Mock<ITenantContext>();
        mockTenant.Setup(t => t.TenantId).Returns(tenantId);
        mockTenant.Setup(t => t.IsHost).Returns(isHost);

        var mockUser = new Mock<ICurrentUser>();
        mockUser.Setup(u => u.UserId).Returns("user_1");
        mockUser.Setup(u => u.IsAuthenticated).Returns(true);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        var context = new AppDbContext(options, mockTime.Object, mockTenant.Object, mockUser.Object);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task Query_Projects_FiltersByTenantIdAutomatically()
    {
        // Arrange
        using (var seedContext = CreateContext("tenant_alpha", isHost: true))
        {
            seedContext.Projects.AddRange(
                new ProjectEntity { Id = Guid.NewGuid(), TenantId = "tenant_alpha", Name = "Alpha 1", ShortCode = "A1", Status = ProjectStatus.Active },
                new ProjectEntity { Id = Guid.NewGuid(), TenantId = "tenant_beta", Name = "Beta 1", ShortCode = "B1", Status = ProjectStatus.Active }
            );
            await seedContext.SaveChangesAsync();
        }

        // Act: Context limited to tenant_alpha queries projects
        using (var queryContext = CreateContext("tenant_alpha", isHost: false))
        {
            var projects = await queryContext.Projects.ToListAsync();

            // Assert
            projects.Should().ContainSingle();
            projects[0].TenantId.Should().Be("tenant_alpha");
            projects[0].Name.Should().Be("Alpha 1");
        }
    }
}
