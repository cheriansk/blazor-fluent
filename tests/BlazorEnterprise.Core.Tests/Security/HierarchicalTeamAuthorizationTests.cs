using System;
using BlazorEnterprise.Core.Domain.Identity;
using BlazorEnterprise.Shared.Attributes;
using BlazorEnterprise.Shared.DataListTypes;
using Xunit;

namespace BlazorEnterprise.Core.Tests.Security;

public class HierarchicalTeamAuthorizationTests
{
    private const string TestIntegritySecret = "test-hmac-integrity-secret-key-32bytes!";

    [Fact]
    public void ProjectRole_NumericHierarchy_MaintainsStrictCumulativeOrder()
    {
        // 10 < 20 < 30 < 40 < 50 < 100
        Assert.True((int)ProjectRoleEnum.ReadOnly < (int)ProjectRoleEnum.Contribute);
        Assert.True((int)ProjectRoleEnum.Contribute < (int)ProjectRoleEnum.Lead);
        Assert.True((int)ProjectRoleEnum.Lead < (int)ProjectRoleEnum.ElevatedLead);
        Assert.True((int)ProjectRoleEnum.ElevatedLead < (int)ProjectRoleEnum.PMO);
        Assert.True((int)ProjectRoleEnum.PMO < (int)ProjectRoleEnum.Admin);

        Assert.Equal(10, (int)ProjectRoleEnum.ReadOnly);
        Assert.Equal(20, (int)ProjectRoleEnum.Contribute);
        Assert.Equal(30, (int)ProjectRoleEnum.Lead);
        Assert.Equal(40, (int)ProjectRoleEnum.ElevatedLead);
        Assert.Equal(50, (int)ProjectRoleEnum.PMO);
        Assert.Equal(100, (int)ProjectRoleEnum.Admin);
    }

    [Fact]
    public void ProgramRole_NumericHierarchy_MirrorsProjectRoleHierarchy()
    {
        Assert.Equal(10, (int)ProgramRoleEnum.ReadOnly);
        Assert.Equal(20, (int)ProgramRoleEnum.Contribute);
        Assert.Equal(30, (int)ProgramRoleEnum.Lead);
        Assert.Equal(40, (int)ProgramRoleEnum.ElevatedLead);
        Assert.Equal(50, (int)ProgramRoleEnum.PMO);
        Assert.Equal(100, (int)ProgramRoleEnum.Admin);
    }

    [Theory]
    [InlineData(ProjectRoleEnum.Admin, ProjectRoleEnum.ReadOnly, true)]
    [InlineData(ProjectRoleEnum.Admin, ProjectRoleEnum.PMO, true)]
    [InlineData(ProjectRoleEnum.PMO, ProjectRoleEnum.ElevatedLead, true)]
    [InlineData(ProjectRoleEnum.ElevatedLead, ProjectRoleEnum.Lead, true)]
    [InlineData(ProjectRoleEnum.Lead, ProjectRoleEnum.Contribute, true)]
    [InlineData(ProjectRoleEnum.Contribute, ProjectRoleEnum.ReadOnly, true)]
    [InlineData(ProjectRoleEnum.Contribute, ProjectRoleEnum.Lead, false)]
    [InlineData(ProjectRoleEnum.ReadOnly, ProjectRoleEnum.Contribute, false)]
    [InlineData(ProjectRoleEnum.Lead, ProjectRoleEnum.PMO, false)]
    public void ProjectRoleExtensions_Satisfies_CorrectlyEvaluatesClearance(
        ProjectRoleEnum userRole,
        ProjectRoleEnum requiredTier,
        bool expectedSatisfies)
    {
        Assert.Equal(expectedSatisfies, userRole.Satisfies(requiredTier));
    }

    [Theory]
    [InlineData(ProjectRoleEnum.ReadOnly, false)]
    [InlineData(ProjectRoleEnum.Contribute, true)]
    [InlineData(ProjectRoleEnum.Lead, true)]
    [InlineData(ProjectRoleEnum.ElevatedLead, true)]
    [InlineData(ProjectRoleEnum.PMO, true)]
    [InlineData(ProjectRoleEnum.Admin, true)]
    public void ProjectRoleExtensions_CanWrite_PermitsContributeAndAboveOnly(ProjectRoleEnum role, bool expectedCanWrite)
    {
        Assert.Equal(expectedCanWrite, role.CanWrite());
    }

    [Theory]
    [InlineData(ProjectRoleEnum.ReadOnly, false)]
    [InlineData(ProjectRoleEnum.Contribute, false)]
    [InlineData(ProjectRoleEnum.Lead, false)]
    [InlineData(ProjectRoleEnum.ElevatedLead, false)]
    [InlineData(ProjectRoleEnum.PMO, false)]
    [InlineData(ProjectRoleEnum.Admin, true)]
    public void ProjectRoleExtensions_CanManageAccess_RestrictedToAdminOnly(ProjectRoleEnum role, bool expectedCanManage)
    {
        Assert.Equal(expectedCanManage, role.CanManageAccess());
    }

    [Fact]
    public void RequiredClearanceAttribute_SingleTier_SetsExpectedViewAndEditTiers()
    {
        var readOnlyAttr = new RequiredClearanceAttribute(ProjectRoleEnum.ReadOnly);
        Assert.Equal(ProjectRoleEnum.ReadOnly, readOnlyAttr.ViewTier);
        Assert.Equal(ProjectRoleEnum.Contribute, readOnlyAttr.EditTier); // ReadOnly can view, Contribute can edit

        var leadAttr = new RequiredClearanceAttribute(ProjectRoleEnum.Lead);
        Assert.Equal(ProjectRoleEnum.Lead, leadAttr.ViewTier);
        Assert.Equal(ProjectRoleEnum.Lead, leadAttr.EditTier); // Lead can view and edit
    }

    [Fact]
    public void RequiredClearanceAttribute_DualTier_DecouplesViewAndEditTiers()
    {
        var dualAttr = new RequiredClearanceAttribute(viewTier: ProjectRoleEnum.Lead, editTier: ProjectRoleEnum.PMO);
        Assert.Equal(ProjectRoleEnum.Lead, dualAttr.ViewTier);
        Assert.Equal(ProjectRoleEnum.PMO, dualAttr.EditTier);

        // A user with Lead clearance can view:
        Assert.True(ProjectRoleEnum.Lead.Satisfies(dualAttr.ViewTier));
        // But cannot edit:
        Assert.False(ProjectRoleEnum.Lead.Satisfies(dualAttr.EditTier));

        // A user with PMO clearance can both view and edit:
        Assert.True(ProjectRoleEnum.PMO.Satisfies(dualAttr.ViewTier));
        Assert.True(ProjectRoleEnum.PMO.Satisfies(dualAttr.EditTier));
    }

    [Fact]
    public void UserEntity_IsValidForLogin_FailsWhenAccountIsInactive()
    {
        var now = DateTime.UtcNow;
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "inactive@company.com",
            FullName = "Inactive User",
            UserType = UserTypeEnum.CompanyUser,
            UserKind = UserKindEnum.Human,
            TimeZoneId = "UTC",
            IsActive = false,
            StartDate = now.AddDays(-10),
            EndDate = now.AddDays(10)
        };

        var isValid = user.IsValidForLogin(now, TestIntegritySecret, out var reason);
        Assert.False(isValid);
        Assert.Contains("deactivated", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UserEntity_IsValidForLogin_FailsWhenAccountIsExpired()
    {
        var now = DateTime.UtcNow;
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "expired@company.com",
            FullName = "Expired User",
            UserType = UserTypeEnum.CompanyUser,
            UserKind = UserKindEnum.Human,
            TimeZoneId = "UTC",
            IsActive = true,
            StartDate = now.AddYears(-2),
            EndDate = now.AddDays(-1) // Expired yesterday
        };

        var isValid = user.IsValidForLogin(now, TestIntegritySecret, out var reason);
        Assert.False(isValid);
        Assert.Contains("expired", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UserEntity_IsValidForLogin_FailsWhenAccountNotStarted()
    {
        var now = DateTime.UtcNow;
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "future@company.com",
            FullName = "Future User",
            UserType = UserTypeEnum.CompanyUser,
            UserKind = UserKindEnum.Human,
            TimeZoneId = "UTC",
            IsActive = true,
            StartDate = now.AddDays(5), // Starts in 5 days
            EndDate = now.AddYears(1)
        };

        var isValid = user.IsValidForLogin(now, TestIntegritySecret, out var reason);
        Assert.False(isValid);
        Assert.Contains("not started", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UserEntity_IsValidForLogin_SucceedsForActiveUnexpiredUser()
    {
        var now = DateTime.UtcNow;
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "active@company.com",
            FullName = "Active User",
            UserType = UserTypeEnum.CompanyUser,
            UserKind = UserKindEnum.Human,
            TimeZoneId = "UTC",
            IsActive = true,
            StartDate = now.AddDays(-10),
            EndDate = now.AddDays(100)
        };
        user.RowSignature = user.ComputeIntegritySignature(TestIntegritySecret);

        var isValid = user.IsValidForLogin(now, TestIntegritySecret, out var reason);
        Assert.True(isValid, reason);
        Assert.True(string.IsNullOrEmpty(reason));
    }
}
