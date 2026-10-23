using System;
using System.Collections.Generic;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Shared.DataListTypes;
using FluentAssertions;
using Xunit;
using TenantInfo = BlazorFluent.Core.Contracts.TenantInfo;

namespace BlazorFluent.Core.Tests.Security;

public class CurrentUserTenancySecurityStateTests
{
    [Fact]
    public void DefaultCurrentUser_Unauthenticated_AllFourStatesAreFalse()
    {
        var tenantContext = new TenantContext();
        var user = new DefaultCurrentUser(tenantContext);

        user.IsAuthenticated.Should().BeFalse();
        user.IsAuthenticated_RegularTenant_User.Should().BeFalse();
        user.IsAuthenticated_RootTenant_User.Should().BeFalse();
        user.IsAuthenticated_RegularTenant_Daemon.Should().BeFalse();
        user.IsAuthenticated_RootTenant_Daemon.Should().BeFalse();
        user.IsAuthenticatedTenantUser.Should().BeFalse();
    }

    [Fact]
    public void DefaultCurrentUser_AuthenticatedInRegularTenant_HumanUser_IsAuthenticated_RegularTenant_User_IsTrue()
    {
        var tenantContext = new TenantContext();
        var tenantId = Guid.NewGuid();
        tenantContext.Initialize(tenantId, "Acme Corp", UserTypeEnum.CompanyUser, new List<TenantInfo>(), "acme", isRootTenant: false);

        var user = new DefaultCurrentUser(tenantContext);
        user.RestoreUserContext("user-123", "alice@acme.com", "Alice Acme", isRootAdmin: false);

        user.IsAuthenticated.Should().BeTrue();
        user.IsAuthenticated_RegularTenant_User.Should().BeTrue();
        user.IsAuthenticated_RootTenant_User.Should().BeFalse();
        user.IsAuthenticated_RegularTenant_Daemon.Should().BeFalse();
        user.IsAuthenticated_RootTenant_Daemon.Should().BeFalse();
        user.IsAuthenticatedTenantUser.Should().BeTrue();
        user.TenantId.Should().Be(tenantId);
        user.TenantSlug.Should().Be("acme");
    }

    [Fact]
    public void DefaultCurrentUser_AuthenticatedInRootTenant_RootAdminUser_IsAuthenticated_RootTenant_User_IsTrue()
    {
        var tenantContext = new TenantContext();
        var rootTenantId = Guid.NewGuid();
        tenantContext.Initialize(rootTenantId, "Root System", UserTypeEnum.CompanyUser, new List<TenantInfo>(), "root", isRootTenant: true);

        var user = new DefaultCurrentUser(tenantContext);
        user.RestoreUserContext("admin-001", "superadmin@platform.internal", "Super Admin", isRootAdmin: true);

        user.IsAuthenticated.Should().BeTrue();
        user.IsAuthenticated_RegularTenant_User.Should().BeFalse();
        user.IsAuthenticated_RootTenant_User.Should().BeTrue();
        user.IsAuthenticated_RegularTenant_Daemon.Should().BeFalse();
        user.IsAuthenticated_RootTenant_Daemon.Should().BeFalse();
        user.IsAuthenticatedTenantUser.Should().BeTrue();
        user.TenantId.Should().Be(rootTenantId);
        user.TenantSlug.Should().Be("root");
    }

    [Fact]
    public void DefaultCurrentUser_BackgroundDaemonInCustomerTenant_IsAuthenticated_RegularTenant_Daemon_IsTrue()
    {
        var tenantContext = new TenantContext();
        var tenantId = Guid.NewGuid();
        tenantContext.Initialize(tenantId, "Acme Corp", UserTypeEnum.CompanyUser, new List<TenantInfo>(), "acme", isRootTenant: false);

        var user = new DefaultCurrentUser(tenantContext);
        user.SetSystemDaemon("AcmeBatchProcessor");

        user.IsAuthenticated.Should().BeTrue();
        user.IsSystemDaemon.Should().BeTrue();
        user.IsAuthenticated_RegularTenant_User.Should().BeFalse();
        user.IsAuthenticated_RootTenant_User.Should().BeFalse();
        user.IsAuthenticated_RegularTenant_Daemon.Should().BeTrue();
        user.IsAuthenticated_RootTenant_Daemon.Should().BeFalse();
        // Router gate strictly denies background daemons
        user.IsAuthenticatedTenantUser.Should().BeFalse();
    }

    [Fact]
    public void DefaultCurrentUser_BackgroundDaemonInRootTenant_IsAuthenticated_RootTenant_Daemon_IsTrue()
    {
        var tenantContext = new TenantContext();
        var rootTenantId = Guid.NewGuid();
        tenantContext.Initialize(rootTenantId, "Root System", UserTypeEnum.CompanyUser, new List<TenantInfo>(), "root", isRootTenant: true);

        var user = new DefaultCurrentUser(tenantContext);
        user.SetSystemDaemon("StartupMigration");

        user.IsAuthenticated.Should().BeTrue();
        user.IsSystemDaemon.Should().BeTrue();
        user.IsAuthenticated_RegularTenant_User.Should().BeFalse();
        user.IsAuthenticated_RootTenant_User.Should().BeFalse();
        user.IsAuthenticated_RegularTenant_Daemon.Should().BeFalse();
        user.IsAuthenticated_RootTenant_Daemon.Should().BeTrue();
        // Router gate strictly denies background daemons
        user.IsAuthenticatedTenantUser.Should().BeFalse();
    }
}
