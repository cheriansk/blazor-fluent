using System;
using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Core.Domain.Identity;
using FluentAssertions;
using Xunit;

namespace BlazorFluent.Core.Tests.Security;

public class UserEntityHmacSealTests
{
    private const string SecretKey = "test_hmac_secret_key_minimum_length_required_32_bytes!!";

    [Fact]
    public void ComputeIntegritySignature_DeterministicPayload_ProducesConsistentSignature()
    {
        // Arrange
        var user = new UserEntity
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Email = "alice@example.com",
            FullName = "Alice Admin",
            UserType = UserTypeEnum.CompanyUser,
            UserKind = UserKindEnum.Human,
            TimeZoneId = "UTC",
            IsActive = true,
            StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc)
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
            Email = "bob@example.com",
            FullName = "Bob Developer",
            UserType = UserTypeEnum.CompanyUser,
            UserKind = UserKindEnum.Human,
            TimeZoneId = "UTC",
            IsActive = true,
            StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        user.RowSignature = user.ComputeIntegritySignature(SecretKey);

        // Act
        var isValid = user.VerifyIntegritySignature(SecretKey);

        // Assert
        isValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("eve@attacker.com", true)] // Tampered email
    [InlineData("bob@example.com", false)] // Tampered active status
    public void VerifyIntegritySignature_TamperedProperties_ReturnsFalse(string modifiedEmail, bool modifiedActive)
    {
        // Arrange
        var user = new UserEntity
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Email = "bob@example.com",
            FullName = "Bob Developer",
            UserType = UserTypeEnum.CompanyUser,
            UserKind = UserKindEnum.Human,
            TimeZoneId = "UTC",
            IsActive = true,
            StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        user.RowSignature = user.ComputeIntegritySignature(SecretKey);

        // Tamper with properties
        user.Email = modifiedEmail;
        user.IsActive = modifiedActive;

        // Act
        var isValid = user.VerifyIntegritySignature(SecretKey);

        // Assert
        isValid.Should().BeFalse();
    }
}
