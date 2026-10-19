using System;
using BlazorFluent.Core.Domain.Identity;
using BlazorFluent.Shared.Attributes;
using BlazorFluent.Shared.DataListTypes;
using Xunit;

namespace BlazorFluent.Core.Tests.Security;

public class HierarchicalTeamAuthorizationTests
{
    private const string TestIntegritySecret = "test-hmac-integrity-secret-key-32bytes!";

    [Fact]
    public void ProjectRole_NumericHierarchy_MaintainsStrictCumulativeOrder()
    {
        // 10 < 20 < 30 < 40 < 50 < 100
        Assert.True((int)ProjectRole.ReadOnly < (int)ProjectRole.Contribute);
        Assert.True((int)ProjectRole.Contribute < (int)ProjectRole.Lead);
        Assert.True((int)ProjectRole.Lead < (int)ProjectRole.ElevatedLead);
        Assert.True((int)ProjectRole.ElevatedLead < (int)ProjectRole.PMO);
        Assert.True((int)ProjectRole.PMO < (int)ProjectRole.Admin);

        Assert.Equal(10, (int)ProjectRole.ReadOnly);
        Assert.Equal(20, (int)ProjectRole.Contribute);
        Assert.Equal(30, (int)ProjectRole.Lead);
        Assert.Equal(40, (int)ProjectRole.ElevatedLead);
        Assert.Equal(50, (int)ProjectRole.PMO);
        Assert.Equal(100, (int)ProjectRole.Admin);
    }

    [Fact]
    public void ProgramRole_NumericHierarchy_MirrorsProjectRoleHierarchy()
    {
        Assert.Equal(10, (int)ProgramRole.ReadOnly);
        Assert.Equal(20, (int)ProgramRole.Contribute);
        Assert.Equal(30, (int)ProgramRole.Lead);
        Assert.Equal(40, (int)ProgramRole.ElevatedLead);
        Assert.Equal(50, (int)ProgramRole.PMO);
        Assert.Equal(100, (int)ProgramRole.Admin);
    }

    [Theory]
    [InlineData(ProjectRole.Admin, ProjectRole.ReadOnly, true)]
    [InlineData(ProjectRole.Admin, ProjectRole.PMO, true)]
    [InlineData(ProjectRole.PMO, ProjectRole.ElevatedLead, true)]
    [InlineData(ProjectRole.ElevatedLead, ProjectRole.Lead, true)]
    [InlineData(ProjectRole.Lead, ProjectRole.Contribute, true)]
    [InlineData(ProjectRole.Contribute, ProjectRole.ReadOnly, true)]
    [InlineData(ProjectRole.Contribute, ProjectRole.Lead, false)]
    [InlineData(ProjectRole.ReadOnly, ProjectRole.Contribute, false)]
    [InlineData(ProjectRole.Lead, ProjectRole.PMO, false)]
    public void ProjectRoleExtensions_Satisfies_CorrectlyEvaluatesClearance(
        ProjectRole userRole,
        ProjectRole requiredTier,
        bool expectedSatisfies)
    {
        Assert.Equal(expectedSatisfies, userRole.Satisfies(requiredTier));
    }

    [Theory]
    [InlineData(ProjectRole.ReadOnly, false)]
    [InlineData(ProjectRole.Contribute, true)]
    [InlineData(ProjectRole.Lead, true)]
    [InlineData(ProjectRole.ElevatedLead, true)]
    [InlineData(ProjectRole.PMO, true)]
    [InlineData(ProjectRole.Admin, true)]
    public void ProjectRoleExtensions_CanWrite_PermitsContributeAndAboveOnly(ProjectRole role, bool expectedCanWrite)
    {
        Assert.Equal(expectedCanWrite, role.CanWrite());
    }

    [Theory]
    [InlineData(ProjectRole.ReadOnly, false)]
    [InlineData(ProjectRole.Contribute, false)]
    [InlineData(ProjectRole.Lead, false)]
    [InlineData(ProjectRole.ElevatedLead, false)]
    [InlineData(ProjectRole.PMO, false)]
    [InlineData(ProjectRole.Admin, true)]
    public void ProjectRoleExtensions_CanManageAccess_RestrictedToAdminOnly(ProjectRole role, bool expectedCanManage)
    {
        Assert.Equal(expectedCanManage, role.CanManageAccess());
    }

    [Fact]
    public void RequiredClearanceAttribute_SingleTier_SetsExpectedViewAndEditTiers()
    {
        var readOnlyAttr = new RequiredClearanceAttribute(ProjectRole.ReadOnly);
        Assert.Equal(ProjectRole.ReadOnly, readOnlyAttr.ViewTier);
        Assert.Equal(ProjectRole.Contribute, readOnlyAttr.EditTier); // ReadOnly can view, Contribute can edit

        var leadAttr = new RequiredClearanceAttribute(ProjectRole.Lead);
        Assert.Equal(ProjectRole.Lead, leadAttr.ViewTier);
        Assert.Equal(ProjectRole.Lead, leadAttr.EditTier); // Lead can view and edit
    }

    [Fact]
    public void RequiredClearanceAttribute_DualTier_DecouplesViewAndEditTiers()
    {
        var dualAttr = new RequiredClearanceAttribute(viewTier: ProjectRole.Lead, editTier: ProjectRole.PMO);
        Assert.Equal(ProjectRole.Lead, dualAttr.ViewTier);
        Assert.Equal(ProjectRole.PMO, dualAttr.EditTier);

        // A user with Lead clearance can view:
        Assert.True(ProjectRole.Lead.Satisfies(dualAttr.ViewTier));
        // But cannot edit:
        Assert.False(ProjectRole.Lead.Satisfies(dualAttr.EditTier));

        // A user with PMO clearance can both view and edit:
        Assert.True(ProjectRole.PMO.Satisfies(dualAttr.ViewTier));
        Assert.True(ProjectRole.PMO.Satisfies(dualAttr.EditTier));
    }

    [Fact]
    public void UserEntity_IsValidForLogin_FailsWhenAccountIsInactive()
    {
        var now = DateTime.UtcNow;
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "inactive@company.com",
            IsActive = false,
            StartDateUtc = now.AddDays(-10),
            EndDateUtc = now.AddDays(10)
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
            IsActive = true,
            StartDateUtc = now.AddYears(-2),
            EndDateUtc = now.AddDays(-1) // Expired yesterday
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
            IsActive = true,
            StartDateUtc = now.AddDays(5), // Starts in 5 days
            EndDateUtc = now.AddYears(1)
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
            IsActive = true,
            StartDateUtc = now.AddDays(-10),
            EndDateUtc = now.AddDays(100)
        };
        user.RowSignature = user.ComputeIntegritySignature(TestIntegritySecret);

        var isValid = user.IsValidForLogin(now, TestIntegritySecret, out var reason);
        Assert.True(isValid, reason);
        Assert.True(string.IsNullOrEmpty(reason));
    }
}
