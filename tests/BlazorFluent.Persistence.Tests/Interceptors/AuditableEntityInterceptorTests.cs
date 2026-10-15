using BlazorFluent.Core.Contracts;
using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Core.Domain.Tasks;
using BlazorFluent.Core.Domain.Tenancy;
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
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private AppDbContext CreateContext(string tenantId, string userId, bool isHost = false)
    {
        var mockTime = new Mock<IDateTimeProvider>();
        mockTime.Setup(t => t.UtcNow).Returns(new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc));

        var mockTenant = new Mock<ITenantContext>();
        mockTenant.Setup(t => t.TenantId).Returns(tenantId);
        mockTenant.Setup(t => t.IsHost).Returns(isHost);

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
        // Arrange
        using var context = CreateContext("tenant_acme", "user_john");

        var project = new ProjectEntity
        {
            Id = Guid.NewGuid(),
            Name = "Apollo Project",
            ShortCode = "APL"
        };

        // Act
        context.Projects.Add(project);
        await context.SaveChangesAsync();

        // Assert
        project.TenantId.Should().Be("tenant_acme");
        project.CreatedBy.Should().Be("user_john@example.com");
        project.Created.Should().Be(new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task SaveChanges_CrossTenantWriteAttempt_ThrowsSecurityException()
    {
        // Arrange: Entity created under tenant_acme
        var projectId = Guid.NewGuid();
        using (var setupContext = CreateContext("tenant_acme", "user_john"))
        {
            setupContext.Projects.Add(new ProjectEntity
            {
                Id = projectId,
                Name = "Alpha Project",
                ShortCode = "ALP"
            });
            await setupContext.SaveChangesAsync();
        }

        // Act: Context for tenant_beta tries to mutate entity from tenant_acme
        using (var attackerContext = CreateContext("tenant_beta", "user_attacker"))
        {
            var entity = new ProjectEntity
            {
                Id = projectId,
                TenantId = "tenant_acme",
                Name = "Hacked Name",
                ShortCode = "ALP"
            };

            attackerContext.Projects.Attach(entity);
            entity.Name = "Cross-Tenant Mutated Name";

            var act = async () => await attackerContext.SaveChangesAsync();

            // Assert
            await act.Should().ThrowAsync<Exception>();
        }
    }
}
