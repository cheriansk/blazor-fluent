using BlazorEnterprise.Api.Admin;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BlazorEnterprise.Core.Tests.Api;

public class ApiSecurityUnitTests
{
    private readonly IConfiguration _config;
    private readonly AdminSecurityService _securityService;

    public ApiSecurityUnitTests()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["Admin:AllowedDomain"] = "company.com",
            ["Admin:KeyVaultUsers"] = "alice@company.com,bob@company.com,charlie@company.com,hacker@company.com" // hacker is 4th, ceiling is 3!
        };

        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        _securityService = new AdminSecurityService(_config, NullLogger<AdminSecurityService>.Instance);
    }

    [Theory]
    [InlineData("alice@company.com", true)]
    [InlineData("bob@company.com", true)]
    [InlineData("charlie@company.com", true)]
    public void IsAuthorizedAdmin_PermitsTop3ConfiguredKeyVaultUsersWithMatchingDomain(string email, bool expected)
    {
        _securityService.IsAuthorizedAdmin(email).Should().Be(expected);
    }

    [Theory]
    [InlineData("hacker@company.com")] // 4th user beyond ceiling of 3
    [InlineData("david@company.com")]  // Not in list
    [InlineData("alice@external.com")] // Mismatched domain
    [InlineData("")]                   // Empty
    [InlineData(null)]                 // Null
    public void IsAuthorizedAdmin_DeniesNonQualifyingUsers(string? email)
    {
        _securityService.IsAuthorizedAdmin(email).Should().BeFalse();
    }

    [Fact]
    public void GetConfiguredAdmins_EnforcesHardCeilingOf3Users()
    {
        var admins = _securityService.GetConfiguredAdmins();
        admins.Should().HaveCount(3);
        admins.Should().Contain("alice@company.com");
        admins.Should().Contain("bob@company.com");
        admins.Should().Contain("charlie@company.com");
        admins.Should().NotContain("hacker@company.com");
    }
}
