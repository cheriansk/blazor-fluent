using BlazorFluent.Core.Contracts;
using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Core.Utilities;
using FluentAssertions;
using Moq;
using Xunit;

namespace BlazorFluent.Core.Tests.Utilities;

public class SystemIdentityUtilityTests
{
    [Fact]
    public void RequireAuthenticatedContext_UnauthenticatedUser_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockUser = new Mock<ICurrentUser>();
        mockUser.Setup(u => u.IsAuthenticated).Returns(false);
        var mockTenant = new Mock<ITenantContext>();
        mockTenant.Setup(t => t.TenantId).Returns(Guid.NewGuid());

        // Act
        var act = () => SystemIdentityUtility.RequireAuthenticatedContext(mockUser.Object, mockTenant.Object, "TestOp");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*requires an established authenticated user identity*");
    }

    [Fact]
    public void RequireAuthenticatedContext_MissingTenantContext_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockUser = new Mock<ICurrentUser>();
        mockUser.Setup(u => u.IsAuthenticated).Returns(true);
        mockUser.Setup(u => u.UserId).Returns("usr_123");
        var mockTenant = new Mock<ITenantContext>();
        mockTenant.Setup(t => t.HasActiveTenant).Returns(false);
        mockTenant.Setup(t => t.TenantId).Throws(new System.Security.SecurityException("No active tenant context."));

        // Act
        var act = () => SystemIdentityUtility.RequireAuthenticatedContext(mockUser.Object, mockTenant.Object, "TestOp");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*requires an active tenant context*");
    }

    [Fact]
    public void RequireAuthenticatedContext_ValidContext_DoesNotThrow()
    {
        // Arrange
        var mockUser = new Mock<ICurrentUser>();
        mockUser.Setup(u => u.IsAuthenticated).Returns(true);
        mockUser.Setup(u => u.UserId).Returns("usr_123");
        var mockTenant = new Mock<ITenantContext>();
        mockTenant.Setup(t => t.TenantId).Returns(Guid.NewGuid());
        mockTenant.Setup(t => t.HasActiveTenant).Returns(true);

        // Act & Assert
        var act = () => SystemIdentityUtility.RequireAuthenticatedContext(mockUser.Object, mockTenant.Object, "TestOp");
        act.Should().NotThrow();
    }

    [Fact]
    public void EstablishPreAuthContext_InitializesDaemonAndSystemTenantWithoutMagicFallback()
    {
        // Arrange
        var mockUser = new Mock<ICurrentUser>();
        var mockTenant = new Mock<ITenantContext>();

        // Act
        SystemIdentityUtility.EstablishPreAuthContext(mockUser.Object, mockTenant.Object, "operator@test.com", "LoginVerify");

        // Assert
        mockUser.Verify(u => u.SetSystemDaemon("PreAuth:LoginVerify", "operator@test.com"), Times.Once);
        mockTenant.Verify(t => t.Initialize(
            SystemIdentityUtility.SystemDaemonTenantId,
            "System Host",
            UserTypeEnum.CompanyUser,
            It.IsAny<TenantInfo[]>(),
            true,
            SystemIdentityUtility.SystemTenantSlug), Times.Once);
    }

    [Fact]
    public void FormatInternalDaemonEmail_ConstructsScopedEmailWithoutAssumedAccounts()
    {
        // Act
        var email = SystemIdentityUtility.FormatInternalDaemonEmail("AuditTrailLogger");

        // Assert
        email.Should().Be("system+audittraillogger@root.internal");
    }

    [Fact]
    public void DefaultCurrentUser_SetSystemDaemon_WithoutServiceEmail_ResolvesDynamicScopedIdentity()
    {
        // Arrange
        var user = new DefaultCurrentUser();

        // Act
        user.SetSystemDaemon("NotificationEngine");

        // Assert
        user.IsSystemDaemon.Should().BeTrue();
        user.UserId.Should().Be("daemon:NotificationEngine");
        user.UserName.Should().Be("NotificationEngine");
        user.Email.Should().Be("system+notificationengine@root.internal");
        user.IsAuthenticated.Should().BeTrue();
    }

    [Theory]
    [InlineData("daemon:JobEngine", true)]
    [InlineData("daemon:AuditTrailLogger", true)]
    [InlineData("system+audit@root.internal", true)]
    [InlineData("system+audit@default.internal", true)]
    [InlineData("worker@subsystem.internal", true)]
    [InlineData("admin@company.com", false)]
    [InlineData("usr_12345", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void IsSystemDaemon_ValidatesIdentityFormat(string? identity, bool expectedResult)
    {
        var result = SystemIdentityUtility.IsSystemDaemon(identity);
        result.Should().Be(expectedResult);
    }
}
