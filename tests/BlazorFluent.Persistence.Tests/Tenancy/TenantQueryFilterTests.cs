using BlazorFluent.Core.Domain.Tenancy;
using System;
using System.Threading.Tasks;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Core.Domain.Tasks;
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
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        _connection.Open();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private AppDbContext CreateContext(Guid? tenantId, bool isHost = false)
    {
        var mockTime = new Mock<IDateTimeProvider>();
        mockTime.Setup(t => t.UtcNow).Returns(DateTime.UtcNow);

        var mockTenant = new Mock<ITenantContext>();
        if (tenantId.HasValue)
        {
            mockTenant.Setup(t => t.TenantId).Returns(tenantId.Value);
            mockTenant.Setup(t => t.HasActiveTenant).Returns(true);
        }
        else
        {
            mockTenant.Setup(t => t.HasActiveTenant).Returns(false);
            mockTenant.Setup(t => t.TenantId).Throws(new System.Security.SecurityException("No active tenant"));
        }
        mockTenant.Setup(t => t.IsRootTenant).Returns(isHost);

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
        var tenantAlpha = Guid.NewGuid();
        var tenantBeta = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Arrange
        using (var seedContext = CreateContext(tenantAlpha, isHost: true))
        {
            seedContext.Projects.AddRange(
                new ProjectEntity { Id = Guid.NewGuid(), TenantId = tenantAlpha, Name = "Alpha 1", ShortCode = "A1", Status = ProjectStatusEnum.Active, StartDate = now, DueDate = now.AddDays(30) },
                new ProjectEntity { Id = Guid.NewGuid(), TenantId = tenantBeta, Name = "Beta 1", ShortCode = "B1", Status = ProjectStatusEnum.Active, StartDate = now, DueDate = now.AddDays(30) }
            );
            await seedContext.SaveChangesAsync();
        }

        // Act: Context limited to tenant_alpha queries projects
        using (var queryContext = CreateContext(tenantAlpha, isHost: false))
        {
            var projects = await queryContext.Projects.ToListAsync();

            // Assert
            projects.Should().ContainSingle();
            projects[0].TenantId.Should().Be(tenantAlpha);
            projects[0].Name.Should().Be("Alpha 1");
        }
    }
}
