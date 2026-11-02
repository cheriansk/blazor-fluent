using BlazorEnterprise.Api.Admin;
using BlazorEnterprise.Core.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlazorEnterprise.Core.Tests.Api;

public class ApiSecurityUnitTests
{
    private readonly AdminSecurityService _securityService;

    public ApiSecurityUnitTests()
    {
        var options = Options.Create(new AdminOptions
        {
            AllowedDomain = "company.com",
            KeyVaultUsers = "alice@company.com,bob@company.com,charlie@company.com,hacker@company.com"
        });

        _securityService = new AdminSecurityService(NullLogger<AdminSecurityService>.Instance, options);
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
        admins.Should().ContainInOrder("alice@company.com", "bob@company.com", "charlie@company.com");
    }

    [Theory]
    [InlineData("https://internal.antigravity.company", true)]
    [InlineData("https://admin.antigravity.company", true)]
    [InlineData("https://malicious.antigravity.company", false)]
    [InlineData("https://evil.com", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsAllowedOrigin_ValidatesOriginAgainstAllowedList(string? origin, bool expected)
    {
        _securityService.IsAllowedOrigin(origin!).Should().Be(expected);
    }
}