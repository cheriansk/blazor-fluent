using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Auditing;
using BlazorEnterprise.Core.Domain.Base;
using BlazorEnterprise.Core.Domain.Calendar;
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

namespace BlazorEnterprise.Persistence.Tests.Versioning;

public class SprintVersionEntityLifecycleTests : IDisposable
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _programId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly SqliteConnection _connection;

    public SprintVersionEntityLifecycleTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        _connection.Open();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private (AppDbContext Context, RevisionService Service, Mock<IDateTimeProvider> MockTime) CreateHarness()
    {
        var mockTenant = new Mock<ITenantContext>();
        mockTenant.Setup(t => t.TenantId).Returns(_tenantId);
        mockTenant.Setup(t => t.HasActiveTenant).Returns(true);
        mockTenant.Setup(t => t.IsRootTenant).Returns(false);

        var mockUser = new Mock<ICurrentUser>();
        mockUser.Setup(u => u.UserId).Returns("usr_lead");
        mockUser.Setup(u => u.Email).Returns("lead@example.com");
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
            new ProgramCalendarRevisionRowProvider(dbFactoryMock.Object),
            new ProjectCalendarRevisionRowProvider(dbFactoryMock.Object)
        };

        var service = new RevisionService(
            dbFactoryMock.Object,
            mockTenant.Object,
            mockUser.Object,
            mockTime.Object,
            NullLogger<RevisionService>.Instance,
            providers);

        var context = new AppDbContext(options, mockTime.Object, mockTenant.Object, mockUser.Object);
        context.Database.EnsureCreated();
        return (context, service, mockTime);
    }

    private ProjectEntity CreateTestProject(DateTime now) => new ProjectEntity
    {
        Id = _projectId,
        TenantId = _tenantId,
        ProgramId = _programId,
        Name = "Sprint Project",
        ShortCode = "SP1",
        Status = ProjectStatusEnum.Active,
        StartDate = now,
        DueDate = now.AddDays(30),
        CreatedBy = "test",
        UpdatedBy = "test"
    };

    [Fact]
    public void VersioningMode_SprintPlan_ShouldBe_LockAndAutoDraft()
    {
        var mode = VersionedFeatureTypeEnumDefinitions.GetVersioningMode(VersionedFeatureTypeEnum.SprintPlan);
        mode.Should().Be(VersioningModeEnum.LockAndAutoDraft);
    }

    [Fact]
    public void VersioningMode_ProgramCalendar_ShouldBe_MultiDraft()
    {
        var mode = VersionedFeatureTypeEnumDefinitions.GetVersioningMode(VersionedFeatureTypeEnum.ProgramCalendar);
        mode.Should().Be(VersioningModeEnum.MultiDraft);
    }

    [Fact]
    public async Task CreateDraftAsync_ForSprintPlan_InitialV1_ShouldSucceed()
    {
        var (context, service, mockTime) = CreateHarness();

        context.Projects.Add(CreateTestProject(mockTime.Object.Now));
        await context.SaveChangesAsync();

        var draft = await service.CreateDraftAsync(
            TaskLevelEnum.Project,
            VersionedFeatureTypeEnum.SprintPlan,
            "Sprint 1 Plan",
            programId: _programId,
            projectId: _projectId);

        draft.Should().NotBeNull();
        draft.VerNum.Should().Be(1);
        draft.Status.Should().Be(RevisionStatusEnum.Draft);
    }

    [Fact]
    public async Task CreateDraftAsync_ForSprintPlan_SubsequentExplicitDraft_ShouldBeRejected()
    {
        var (context, service, mockTime) = CreateHarness();

        context.Projects.Add(CreateTestProject(mockTime.Object.Now));
        await context.SaveChangesAsync();

        // Initial draft
        await service.CreateDraftAsync(
            TaskLevelEnum.Project,
            VersionedFeatureTypeEnum.SprintPlan,
            "Sprint 1 Plan",
            programId: _programId,
            projectId: _projectId);

        // Attempting to manually create a second arbitrary draft should throw
        var act = async () => await service.CreateDraftAsync(
            TaskLevelEnum.Project,
            VersionedFeatureTypeEnum.SprintPlan,
            "Sprint 1 Rogues Draft",
            programId: _programId,
            projectId: _projectId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Explicit draft creation is not permitted*");
    }

    [Fact]
    public async Task FinalizeAsync_WithoutApproval_ShouldThrowException()
    {
        var (context, service, mockTime) = CreateHarness();

        context.Projects.Add(CreateTestProject(mockTime.Object.Now));
        await context.SaveChangesAsync();

        var initialDraft = await service.CreateDraftAsync(
            TaskLevelEnum.Project,
            VersionedFeatureTypeEnum.SprintPlan,
            "Sprint 1 Initial",
            programId: _programId,
            projectId: _projectId);

        // Finalizing directly from Draft without review and approval must be rejected
        var act = async () => await service.FinalizeAsync(initialDraft.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*A sprint plan draft cannot be finalized until it has progressed through the formal review and approval process*");
    }

    [Fact]
    public async Task FullSprintLifecycle_Review_Approve_Finalize_CloseSprint_ShouldSucceed()
    {
        var (context, service, mockTime) = CreateHarness();

        context.Projects.Add(CreateTestProject(mockTime.Object.Now));
        await context.SaveChangesAsync();

        // 1. Initial Draft v1
        var draft = await service.CreateDraftAsync(
            TaskLevelEnum.Project,
            VersionedFeatureTypeEnum.SprintPlan,
            "Sprint 1 Scope",
            programId: _programId,
            projectId: _projectId);

        draft.Status.Should().Be(RevisionStatusEnum.Draft);

        // 2. Submit for Review
        var underReview = await service.SubmitForReviewAsync(draft.Id, "reviewer@example.com", "Please review sprint 1");
        underReview.Status.Should().Be(RevisionStatusEnum.ReadyForReview);

        // 3. Approve Revision
        var approved = await service.ApproveRevisionAsync(underReview.Id, "Scope confirmed");
        approved.Status.Should().Be(RevisionStatusEnum.Approved);

        // 4. Finalize Baseline
        var finalized = await service.FinalizeAsync(approved.Id, "Finalizing committed scope");
        finalized.FinalizedBaseline.Status.Should().Be(RevisionStatusEnum.Active);
        finalized.FinalizedBaseline.VerNum.Should().Be(1);

        finalized.WorkingDraft.Status.Should().Be(RevisionStatusEnum.Draft);
        finalized.WorkingDraft.VerNum.Should().Be(2);
        finalized.WorkingDraft.ParentVerNum.Should().Be(1);
        finalized.WorkingDraft.ParentRevisionHeaderId.Should().Be(approved.Id);

        // 5. Close Sprint
        var closedWorkingDraft = await service.CloseCycleAsync(finalized.WorkingDraft.Id, "Sprint complete");
        closedWorkingDraft.Status.Should().Be(RevisionStatusEnum.DraftComplete);
        closedWorkingDraft.VerNum.Should().Be(2);

        // Baseline must be marked Complete
        var baseline = await context.RevisionHeaders.FindAsync(finalized.FinalizedBaseline.Id);
        baseline!.Status.Should().Be(RevisionStatusEnum.Complete);
    }
}
