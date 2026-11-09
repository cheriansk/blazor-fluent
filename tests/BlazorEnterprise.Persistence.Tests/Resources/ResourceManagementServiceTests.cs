using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Identity;
using BlazorEnterprise.Core.Domain.Resources;
using BlazorEnterprise.Core.Domain.Tasks;
using BlazorEnterprise.Core.Domain.Tenancy;
using BlazorEnterprise.Core.Domain.Versioning;
using BlazorEnterprise.Core.Models;
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

namespace BlazorEnterprise.Persistence.Tests.Resources;

public class ResourceManagementServiceTests : IDisposable
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _programId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly SqliteConnection _connection;

    public ResourceManagementServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        _connection.Open();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private (AppDbContext Context, ResourceManagementService ResourceService, RevisionService RevisionService) CreateHarness()
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
            new ResourceAllocationRevisionRowProvider(dbFactoryMock.Object)
        };

        var revisionService = new RevisionService(
            dbFactoryMock.Object,
            mockTenant.Object,
            mockUser.Object,
            mockTime.Object,
            NullLogger<RevisionService>.Instance,
            providers);

        var resourceService = new ResourceManagementService(
            dbFactoryMock.Object,
            revisionService,
            mockTenant.Object,
            mockUser.Object,
            mockTime.Object,
            NullLogger<ResourceManagementService>.Instance);

        var context = new AppDbContext(options, mockTime.Object, mockTenant.Object, mockUser.Object);
        context.Database.EnsureCreated();
        return (context, resourceService, revisionService);
    }

    private (ProjectEntity Project, UserEntity User) SeedData(AppDbContext context)
    {
        var now = DateTime.UtcNow;
        var project = new ProjectEntity
        {
            Id = _projectId,
            TenantId = _tenantId,
            ProgramId = _programId,
            Name = "Apollo Resources Project",
            ShortCode = "APR",
            Status = ProjectStatusEnum.Active,
            StartDate = now,
            DueDate = now.AddDays(7),
            CreatedBy = "usr_pm",
            UpdatedBy = "usr_pm"
        };

        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "developer@example.com",
            FullName = "Dev User",
            UserType = UserTypeEnum.CompanyUser,
            UserKind = UserKindEnum.Human,
            StartDate = now,
            IsActive = true,
            TimeZoneId = "UTC",
            CreatedBy = "usr_pm",
            UpdatedBy = "usr_pm"
        };

        var tenantUser = new TenantUserEntity
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            UserEntityId = user.Id,
            Role = TenantRoleEnum.TenantMember,
            IsActive = true,
            CreatedBy = "usr_pm",
            UpdatedBy = "usr_pm"
        };

        context.Projects.Add(project);
        context.Users.Add(user);
        context.TenantUsers.Add(tenantUser);
        context.SaveChanges();

        return (project, user);
    }

    [Fact]
    public async Task AddResourceAsync_ShouldAssignUserToProject()
    {
        var (context, service, _) = CreateHarness();
        var (_, user) = SeedData(context);

        var resource = await service.AddResourceAsync(_projectId, user.Id, ProjectResourceRoleEnum.Developer);

        resource.Should().NotBeNull();
        resource.Role.Should().Be(ProjectResourceRoleEnum.Developer);

        var resources = await service.GetProjectResourcesAsync(_projectId);
        resources.Should().HaveCount(1);
        resources[0].UserName.Should().Be("Dev User");
    }

    [Fact]
    public async Task AllocationPlan_Draft_SetCellAndSummary_ShouldComputeHoursAndCost()
    {
        var (context, service, _) = CreateHarness();
        var (project, user) = SeedData(context);

        var resource = await service.AddResourceAsync(_projectId, user.Id, ProjectResourceRoleEnum.Developer);
        var plan = await service.GetOrCreateDraftAllocationPlanAsync(_projectId);

        var allocDate = DateOnly.FromDateTime(project.StartDate);
        await service.SetResourceAllocationAsync(plan.Id, resource.Id, allocDate, 100);
        await service.SetResourceRateAsync(plan.Id, resource.Id, 150m);

        var summary = await service.GetResourcePlanSummaryAsync(_projectId, plan.Id);

        summary.Should().NotBeNull();
        summary.VerNum.Should().Be(1);
        summary.Resources.Should().HaveCount(1);
        summary.Resources[0].TotalAllocatedHours.Should().Be(8.0);
        summary.Resources[0].TotalCost.Should().Be(1200m); // 8 hrs * $150
        summary.TotalPlanCost.Should().Be(1200m);
    }
}
