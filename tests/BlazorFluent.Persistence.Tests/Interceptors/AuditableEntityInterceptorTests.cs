using BlazorFluent.Core.Domain.Tenancy;
using System;
using System.Threading.Tasks;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Core.Domain.Tasks;
using BlazorFluent.Persistence.Context;
using BlazorFluent.Persistence.Interceptors;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace BlazorFluent.Persistence.Tests.Interceptors;

public class AuditableEntityInterceptorTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public AuditableEntityInterceptorTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        _connection.Open();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private AppDbContext CreateContext(Guid? tenantId, string userId, bool isHost = false)
    {
        var mockTime = new Mock<IDateTimeProvider>();
        var testTime = new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
        mockTime.Setup(t => t.UtcNow).Returns(testTime);
        mockTime.Setup(t => t.Now).Returns(testTime);

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
        mockUser.Setup(u => u.UserId).Returns(userId);
        mockUser.Setup(u => u.Email).Returns($"{userId}@example.com");
        mockUser.Setup(u => u.IsAuthenticated).Returns(true);

        var interceptor = new AuditableEntityInterceptor();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(interceptor)
            .Options;

        var context = new AppDbContext(options, mockTime.Object, mockTenant.Object, mockUser.Object);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task SaveChanges_NewTenantEntity_StampsTenantIdAndAuditFields()
    {
        var tenantAcme = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var now = DateTime.UtcNow;

        // Arrange
        using var context = CreateContext(tenantAcme, "user_john");

        var project = new ProjectEntity
        {
            Id = Guid.NewGuid(),
            Name = "Apollo Project",
            ShortCode = "APL",
            Status = ProjectStatusEnum.Active,
            StartDate = now,
            DueDate = now.AddDays(30)
        };

        // Act
        context.Projects.Add(project);
        await context.SaveChangesAsync();

        // Assert
        project.TenantId.Should().Be(tenantAcme);
        project.CreatedBy.Should().Be("user_john");
        project.Created.Should().Be(new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task SaveChanges_CrossTenantWriteAttempt_ThrowsSecurityException()
    {
        var tenantAcme = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var tenantBeta = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var now = DateTime.UtcNow;

        // Arrange: Entity created under tenant_acme
        var projectId = Guid.NewGuid();
        using (var setupContext = CreateContext(tenantAcme, "user_john"))
        {
            setupContext.Projects.Add(new ProjectEntity
            {
                Id = projectId,
                Name = "Alpha Project",
                ShortCode = "ALP",
                Status = ProjectStatusEnum.Active,
                StartDate = now,
                DueDate = now.AddDays(30)
            });
            await setupContext.SaveChangesAsync();
        }

        // Act: Context for tenant_beta tries to mutate entity from tenant_acme
        using (var attackerContext = CreateContext(tenantBeta, "user_attacker"))
        {
            var entity = new ProjectEntity
            {
                Id = projectId,
                TenantId = tenantAcme,
                Name = "Hacked Name",
                ShortCode = "ALP",
                Status = ProjectStatusEnum.Active,
                StartDate = now,
                DueDate = now.AddDays(30)
            };

            attackerContext.Projects.Attach(entity);
            entity.Name = "Cross-Tenant Mutated Name";

            var act = async () => await attackerContext.SaveChangesAsync();

            // Assert
            await act.Should().ThrowAsync<Exception>();
        }
    }
}
