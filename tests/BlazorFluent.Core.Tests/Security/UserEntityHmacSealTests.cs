using System.Security.Cryptography;
using BlazorFluent.Core.Domain.Identity;
using FluentAssertions;
using Xunit;

namespace BlazorFluent.Core.Tests.Security;

public class UserEntityHmacSealTests
{
    private const string SecretKey = "test_cryptographic_secret_key_minimum_32_bytes_long!";

    [Fact]
    public void ComputeIntegritySignature_ValidUser_GeneratesConsistentSignature()
    {
        // Arrange
        var user = new UserEntity
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            DefaultTenantId = "tenant_alpha",
            Email = "alice@example.com",
            IsActive = true,
            StartDateUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDateUtc = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        // Act
        var sig1 = user.ComputeIntegritySignature(SecretKey);
        var sig2 = user.ComputeIntegritySignature(SecretKey);

        // Assert
        sig1.Should().NotBeNullOrWhiteSpace();
        sig1.Should().Be(sig2);
    }

    [Fact]
    public void VerifyIntegritySignature_MatchingRowSignature_ReturnsTrue()
    {
        // Arrange
        var user = new UserEntity
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            DefaultTenantId = "tenant_beta",
            Email = "bob@example.com",
            IsActive = true,
            StartDateUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDateUtc = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        user.RowSignature = user.ComputeIntegritySignature(SecretKey);

        // Act
        var isValid = user.VerifyIntegritySignature(SecretKey);

        // Assert
        isValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("eve@attacker.com", true, "tenant_beta")] // Tampered email
    [InlineData("bob@example.com", false, "tenant_beta")] // Tampered active status
    [InlineData("bob@example.com", true, "tenant_malicious")] // Tampered tenant
    public void VerifyIntegritySignature_TamperedProperties_ReturnsFalse(string modifiedEmail, bool modifiedActive, string modifiedTenant)
    {
        // Arrange
        var user = new UserEntity
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            DefaultTenantId = "tenant_beta",
            Email = "bob@example.com",
            IsActive = true,
            StartDateUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDateUtc = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        user.RowSignature = user.ComputeIntegritySignature(SecretKey);

        // Tamper with properties
        user.Email = modifiedEmail;
        user.IsActive = modifiedActive;
        user.DefaultTenantId = modifiedTenant;

        // Act
        var isValid = user.VerifyIntegritySignature(SecretKey);

        // Assert
        isValid.Should().BeFalse();
    }
}
