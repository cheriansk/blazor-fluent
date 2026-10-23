using System;
using System.Collections.Generic;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.Domain.Tenancy;
using FluentAssertions;
using Moq;
using Xunit;

namespace BlazorFluent.Core.Tests.Tenancy;

public class TenantEntityValidatorTests
{
    private readonly Guid _rootTenantId = Guid.NewGuid();
    private readonly string _rootTenantSlug = "root-system";
    private readonly Mock<IRootAdminService> _mockRootAdminService;

    public TenantEntityValidatorTests()
    {
        _mockRootAdminService = new Mock<IRootAdminService>();
        _mockRootAdminService.Setup(s => s.RootTenantId).Returns(_rootTenantId);
        _mockRootAdminService.Setup(s => s.RootTenantSlug).Returns(_rootTenantSlug);
        _mockRootAdminService.Setup(s => s.RootTenantName).Returns("Root Administration");
        _mockRootAdminService.Setup(s => s.GetRootAdminEmails()).Returns(new[] { "admin1@platform.com", "admin2@platform.com" });
    }

    [Fact]
    public void Validate_StandardTenant_WithValidExternalAndInternalDomains_Passes()
    {
        var validator = new TenantEntityValidator(_mockRootAdminService.Object);
        var tenant = new TenantEntity
        {
            Id = Guid.NewGuid(),
            Slug = "client-corp",
            Name = "Client Corporation",
            InternalEmailDomains = "@clientcorp.com",
            ExternalEmailDomains = "@partner.com",
            StartDate = DateTime.UtcNow.AddDays(-30)
        };

        var result = validator.Validate(tenant);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_StandardTenant_WithoutExternalDomains_Fails()
    {
        var validator = new TenantEntityValidator(_mockRootAdminService.Object);
        var tenant = new TenantEntity
        {
            Id = Guid.NewGuid(),
            Slug = "client-corp",
            Name = "Client Corporation",
            InternalEmailDomains = "@clientcorp.com",
            ExternalEmailDomains = "", // Missing external domains
            StartDate = DateTime.UtcNow.AddDays(-30)
        };

        var result = validator.Validate(tenant);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ExternalEmailDomains");
    }

    [Fact]
    public void Validate_RootTenant_WithExternalDomains_Fails()
    {
        var validator = new TenantEntityValidator(_mockRootAdminService.Object);
        var tenant = new TenantEntity
        {
            Id = _rootTenantId,
            Slug = _rootTenantSlug,
            Name = "Root Administration",
            InternalEmailDomains = "@platform.com",
            ExternalEmailDomains = "@external.com", // Disallowed for Root Tenant
            StartDate = DateTime.UtcNow.AddDays(-30)
        };

        var result = validator.Validate(tenant);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Root Tenant cannot have external email domains"));
    }

    [Fact]
    public void Validate_RootTenant_WithNoExternalDomains_AndMatchingAdminDomains_Passes()
    {
        var validator = new TenantEntityValidator(_mockRootAdminService.Object);
        var tenant = new TenantEntity
        {
            Id = _rootTenantId,
            Slug = _rootTenantSlug,
            Name = "Root Administration",
            InternalEmailDomains = "@platform.com",
            ExternalEmailDomains = "",
            StartDate = DateTime.UtcNow.AddDays(-30)
        };

        var result = validator.Validate(tenant);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_RootTenant_WhenRootAdminEmailDomainMismatch_Fails()
    {
        _mockRootAdminService.Setup(s => s.GetRootAdminEmails())
            .Returns(new[] { "admin1@platform.com", "rogue@external-unrelated.com" });

        var validator = new TenantEntityValidator(_mockRootAdminService.Object);
        var tenant = new TenantEntity
        {
            Id = _rootTenantId,
            Slug = _rootTenantSlug,
            Name = "Root Administration",
            InternalEmailDomains = "@platform.com",
            ExternalEmailDomains = "",
            StartDate = DateTime.UtcNow.AddDays(-30)
        };

        var result = validator.Validate(tenant);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("All root administrator emails must belong to the Root Tenant's internal email domain(s)"));
    }
}
