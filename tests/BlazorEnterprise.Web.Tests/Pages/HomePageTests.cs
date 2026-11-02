using BlazorEnterprise.Components.Pages;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Auditing;
using BlazorEnterprise.Core.Domain.Tasks;
using BlazorEnterprise.Core.Domain.Tenancy;
using BlazorEnterprise.Core.Dtos.Response;
using BlazorEnterprise.Shared.Contracts.Common;
using BlazorEnterprise.Shared.DataListTypes;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BlazorEnterprise.Web.Tests.Pages;

public class HomePageTests
{
    private readonly Mock<ITenantService> _tenantServiceMock = new();
    private readonly Mock<IUserTaskService> _taskServiceMock = new();
    private readonly Mock<IAuditService> _auditServiceMock = new();
    private readonly Mock<ITenantContext> _tenantContextMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly Mock<IProgramService> _programServiceMock = new();
    private readonly Mock<IProjectAuthorizationService> _projectAuthMock = new();
    private readonly Mock<IProjectHealthService> _projectHealthMock = new();
    private readonly Mock<IProjectContext> _projectContextMock = new();
    private readonly Mock<ILogger<HomePage>> _loggerMock = new();

    private HomePage CreateHomePage()
    {
        return new HomePage
        {
            TenantService = _tenantServiceMock.Object,
            TaskService = _taskServiceMock.Object,
            AuditService = _auditServiceMock.Object,
            TenantContext = _tenantContextMock.Object,
            CurrentUser = _currentUserMock.Object,
            ProgramService = _programServiceMock.Object,
            ProjectAuthService = _projectAuthMock.Object,
            ProjectHealthService = _projectHealthMock.Object,
            ProjectContext = _projectContextMock.Object,
            Logger = _loggerMock.Object
        };
    }

    [Fact]
    public async Task HomePage_WhenRegularTenantUser_LoadsProgramHealthAndAuthorizedProjectCards()
    {
        // Arrange
        var tenantGuid = Guid.NewGuid();
        var programGuid = Guid.NewGuid();
        var proj1Guid = Guid.NewGuid();
        var proj2Guid = Guid.NewGuid();
        const string tenantSlug = "acme-corp";

        _tenantContextMock.Setup(t => t.TenantSlug).Returns(tenantSlug);
        _tenantContextMock.Setup(t => t.TenantId).Returns(tenantGuid);
        _currentUserMock.Setup(u => u.IsAuthenticated_RegularTenant_User).Returns(true);
        _currentUserMock.Setup(u => u.IsAuthenticated_RootTenant_User).Returns(false);

        var program = new ProgramEntity
        {
            Id = programGuid,
            TenantId = tenantGuid,
            Name = "Digital Transformation Program",
            Code = "PRG-DTP",
            Status = ProgramStatusEnum.Active,
            StartDate = DateTime.UtcNow.AddMonths(-3),
            DueDate = DateTime.UtcNow.AddMonths(9)
        };

        var programHealth = new ProgramHealthSummaryDto
        {
            ProgramId = programGuid,
            ProgramName = program.Name,
            ProgramCode = program.Code,
            Status = ProgramStatusEnum.Active,
            Health = ProgramHealthRatingEnum.OnTrack,
            TotalProjects = 2,
            ActiveProjects = 2,
            TotalTasks = 20,
            CompletedTasks = 15,
            OpenTasks = 5,
            OverdueTasks = 0
        };

        _programServiceMock
            .Setup(p => p.GetProgramsAsync(tenantSlug, default))
            .ReturnsAsync(new List<ProgramEntity> { program });

        _programServiceMock
            .Setup(p => p.GetProgramHealthAsync(programGuid, default))
            .ReturnsAsync(programHealth);

        var allTenantProjects = new List<ProjectEntity>
        {
            new ProjectEntity
            {
                Id = proj1Guid,
                TenantId = tenantGuid,
                Name = "Core API Modernization",
                ShortCode = "API-MOD",
                Status = ProjectStatusEnum.Active,
                ProgramId = programGuid,
                Program = program,
                StartDate = DateTime.UtcNow.AddMonths(-2),
                DueDate = DateTime.UtcNow.AddMonths(4)
            },
            new ProjectEntity
            {
                Id = proj2Guid,
                TenantId = tenantGuid,
                Name = "Customer Portal Redesign",
                ShortCode = "PORTAL",
                Status = ProjectStatusEnum.Active,
                ProgramId = programGuid,
                Program = program,
                StartDate = DateTime.UtcNow.AddMonths(-1),
                DueDate = DateTime.UtcNow.AddMonths(2)
            }
        };

        _tenantServiceMock
            .Setup(t => t.GetTenantProjectsAsync(tenantSlug, default))
            .ReturnsAsync(allTenantProjects);

        // User is only authorized on Project 1
        _projectAuthMock
            .Setup(a => a.GetAuthorizedProjectIdsAsync(ProjectRoleEnum.ReadOnly, default))
            .ReturnsAsync(new List<Guid> { proj1Guid });

        _projectAuthMock
            .Setup(a => a.GetCurrentUserProjectRoleAsync(proj1Guid, default))
            .ReturnsAsync(ProjectRoleEnum.Admin);

        _projectHealthMock
            .Setup(h => h.GetProjectHealthAsync(proj1Guid, default))
            .ReturnsAsync(new ProjectHealthSummaryDto
            {
                ProjectId = proj1Guid,
                ProjectName = "Core API Modernization",
                Health = ProjectHealthRatingEnum.OnTrack,
                TotalTasks = 10,
                CompletedTasks = 8,
                InProgressTasks = 2,
                OpenTasks = 2
            });

        _taskServiceMock
            .Setup(t => t.GetRecentTasksAsync(30, default))
            .ReturnsAsync(new List<TaskEntity>());

        _auditServiceMock
            .Setup(a => a.GetAuditTrailAsync(It.IsAny<AuditTrailFilterReqDto>(), default))
            .ReturnsAsync(new PagedResultRespDto<AuditRecordEntity>
            {
                Items = new List<AuditRecordEntity>(),
                TotalCount = 0,
                PageNumber = 1,
                PageSize = 5
            });

        var page = CreateHomePage();

        // Act
        await page.InitializeForTestAsync();

        // Assert
        page.IsLoading.Should().BeFalse();
        page.IsRootAdminOnly.Should().BeFalse();

        // Verify Program Health Summary
        page.ProgramHealth.Should().NotBeNull();
        page.ProgramHealth!.ProgramName.Should().Be("Digital Transformation Program");
        page.ProgramHealth.ProgressPercentage.Should().Be(75.0);
        page.ProgramHealth.Health.Should().Be(ProgramHealthRatingEnum.OnTrack);

        // Verify User Projects: MUST ONLY contain the authorized Project 1 (not Project 2)
        page.UserProjects.Should().HaveCount(1);
        var card = page.UserProjects[0];
        card.Id.Should().Be(proj1Guid);
        card.Name.Should().Be("Core API Modernization");
        card.ShortCode.Should().Be("API-MOD");
        card.UserRole.Should().Be(ProjectRoleEnum.Admin);
        card.Health.Should().Be(ProjectHealthRatingEnum.OnTrack);
        card.ProgressPercentage.Should().Be(80.0);
        card.CompletedTasks.Should().Be(8);
        card.TotalTasks.Should().Be(10);
    }

    [Fact]
    public async Task HomePage_WhenRootAdmin_EnforcesZeroCrossTenantData()
    {
        // Arrange
        _currentUserMock.Setup(u => u.IsAuthenticated_RegularTenant_User).Returns(false);
        _currentUserMock.Setup(u => u.IsAuthenticated_RootTenant_User).Returns(true);

        var page = CreateHomePage();

        // Act
        await page.InitializeForTestAsync();

        // Assert
        page.IsLoading.Should().BeFalse();
        page.IsRootAdminOnly.Should().BeTrue();
        page.UserProjects.Should().BeEmpty();
        page.Programs.Should().BeEmpty();
        page.ProgramHealth.Should().BeNull();
    }

    [Fact]
    public async Task HomePage_WhenUserHasNoAssignedProjects_ReturnsEmptyListWithoutMockFallback()
    {
        // Arrange
        const string tenantSlug = "omega-inc";
        _tenantContextMock.Setup(t => t.TenantSlug).Returns(tenantSlug);
        _currentUserMock.Setup(u => u.IsAuthenticated_RegularTenant_User).Returns(true);
        _currentUserMock.Setup(u => u.IsAuthenticated_RootTenant_User).Returns(false);

        _programServiceMock
            .Setup(p => p.GetProgramsAsync(tenantSlug, default))
            .ReturnsAsync(new List<ProgramEntity>());

        _projectAuthMock
            .Setup(a => a.GetAuthorizedProjectIdsAsync(ProjectRoleEnum.ReadOnly, default))
            .ReturnsAsync(new List<Guid>()); // 0 assigned projects

        _tenantServiceMock
            .Setup(t => t.GetTenantProjectsAsync(tenantSlug, default))
            .ReturnsAsync(new List<ProjectEntity>());

        _taskServiceMock
            .Setup(t => t.GetRecentTasksAsync(30, default))
            .ReturnsAsync(new List<TaskEntity>());

        _auditServiceMock
            .Setup(a => a.GetAuditTrailAsync(It.IsAny<AuditTrailFilterReqDto>(), default))
            .ReturnsAsync(new PagedResultRespDto<AuditRecordEntity>
            {
                Items = new List<AuditRecordEntity>(),
                TotalCount = 0,
                PageNumber = 1,
                PageSize = 5
            });

        var page = CreateHomePage();

        // Act
        await page.InitializeForTestAsync();

        // Assert
        page.IsLoading.Should().BeFalse();
        page.UserProjects.Should().BeEmpty();
        page.Programs.Should().BeEmpty();
        page.ProgramHealth.Should().BeNull();
    }
}
