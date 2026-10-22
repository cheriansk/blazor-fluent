using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.Dtos.Response;
using BlazorFluent.Core.Security;
using BlazorFluent.Infrastructure.Security;
using BlazorFluent.Shared.Contracts.Engine.Client;
using BlazorFluent.Shared.DataListTypes;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BlazorFluent.Core.Tests.Security;

public class TenantContextRequiredTenantIdTests
{
    [Fact]
    public void RequiredTenantId_WhenInitializedWithValidGuid_ReturnsGuid()
    {
        // Arrange
        var context = new TenantContext();
        var tenantId = Guid.CreateVersion7();
        var allowedTenants = new List<TenantInfo>
        {
            new TenantInfo(tenantId, "Acme Corp", "acme", true)
        };

        // Act
        context.Initialize(tenantId, "Acme Corp", UserTypeEnum.CompanyUser, allowedTenants, isHost: false, tenantSlug: "acme");

        // Assert
        context.TenantId.Should().Be(tenantId);
        context.RequiredTenantId.Should().Be(tenantId);
    }

    [Fact]
    public void RequiredTenantId_WhenUninitialized_ThrowsZeroTrustInvalidOperationException()
    {
        // Arrange
        var context = new TenantContext();

        // Act & Assert
        context.HasActiveTenant.Should().BeFalse();
        var act = () => context.TenantId;
        act.Should().Throw<System.Security.SecurityException>();
    }

    [Fact]
    public void RequiredTenantId_WhenInitializedWithGuidEmpty_ThrowsZeroTrustInvalidOperationException()
    {
        // Arrange
        var context = new TenantContext();

        // Act
        var act = () => context.Initialize(Guid.Empty, "Invalid Tenant", UserTypeEnum.CompanyUser, []);
        act.Should().Throw<System.Security.SecurityException>();
    }

    [Fact]
    public void RequiredTenantId_AfterReset_ThrowsZeroTrustInvalidOperationException()
    {
        // Arrange
        var context = new TenantContext();
        var tenantId = Guid.CreateVersion7();
        context.Initialize(tenantId, "Acme Corp", UserTypeEnum.CompanyUser, [new TenantInfo(tenantId, "Acme Corp", "acme", true)]);

        // Act
        context.Reset();

        // Assert
        context.HasActiveTenant.Should().BeFalse();
        var act = () => context.TenantId;
        act.Should().Throw<System.Security.SecurityException>();
    }

    [Fact]
    public void TenantTokenService_GenerateS2SToken_WithGuid_SucceedsAndCarriesGuidClaim()
    {
        // Arrange
        var tenantGuid = Guid.CreateVersion7();
        var tenantIdStr = tenantGuid.ToString();
        var licenseKey = OpaqueIdUtility.GenerateSecureLicenseKey();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"Tenants:{tenantIdStr}:LicenseKey"] = licenseKey
            })
            .Build();

        var correlationContext = new CorrelationContext { CorrelationId = "corr-" + Guid.NewGuid() };
        var tokenService = new TenantTokenService(config, correlationContext);

        // Act
        var token = tokenService.GenerateS2SToken(tenantGuid, "BatchWorker");

        // Assert
        token.Should().NotBeNullOrWhiteSpace();

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Claims.Should().Contain(c => c.Type == "tid" && c.Value == tenantIdStr);
        jwt.Claims.Should().Contain(c => c.Type == "tenant_id" && c.Value == tenantIdStr);
    }

    [Fact]
    public void TenantTokenService_GenerateS2SToken_WithGuidEmpty_ThrowsZeroTrustException()
    {
        // Arrange
        var config = new ConfigurationBuilder().Build();
        var correlationContext = new CorrelationContext { CorrelationId = "corr-1" };
        var tokenService = new TenantTokenService(config, correlationContext);

        // Act & Assert
        var act = () => tokenService.GenerateS2SToken(Guid.Empty, "BatchWorker");
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Zero-Trust Security Violation: Cannot mint an S2S token without an established TenantId.*");
    }
}
