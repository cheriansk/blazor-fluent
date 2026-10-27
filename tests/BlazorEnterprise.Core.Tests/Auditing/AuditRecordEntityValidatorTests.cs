using BlazorEnterprise.Core.Domain.Auditing;
using BlazorEnterprise.Shared.DataListTypes;
using FluentAssertions;
using Xunit;

namespace BlazorEnterprise.Core.Tests.Auditing;

public class AuditRecordEntityValidatorTests
{
    private readonly AuditRecordEntityValidator _validator = new();

    [Fact]
    public void Validate_ValidEntityChangeRecord_Passes()
    {
        // Arrange
        var record = new AuditRecordEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = Guid.NewGuid(),
            EventType = AuditEventTypeEnum.EntityChange,
            Severity = AuditSeverityEnum.Information,
            EntityName = "ProjectEntity",
            EntityId = Guid.NewGuid().ToString(),
            Operation = EntityOperationEnum.Update,
            ChangesJson = "{\"Name\":{\"Old\":\"Project A\",\"New\":\"Project B\"}}",
            UserId = "usr_456",
            UserEmail = "architect@acme.com",
            UserType = UserTypeEnum.CompanyUser,
            Created = DateTime.UtcNow,
            CreatedBy = "usr_456"
        };

        // Act
        var result = _validator.Validate(record);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_EntityChangeMissingEntityName_Fails()
    {
        // Arrange
        var record = new AuditRecordEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = Guid.NewGuid(),
            EventType = AuditEventTypeEnum.EntityChange,
            Severity = AuditSeverityEnum.Information,
            EntityName = "", // Invalid
            EntityId = "proj_123",
            Operation = EntityOperationEnum.Insert,
            ChangesJson = "{}"
        };

        // Act
        var result = _validator.Validate(record);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(AuditRecordEntity.EntityName));
    }

    [Fact]
    public void Validate_EntityChangeMissingChangesJson_Fails()
    {
        // Arrange
        var record = new AuditRecordEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = Guid.NewGuid(),
            EventType = AuditEventTypeEnum.EntityChange,
            Severity = AuditSeverityEnum.Information,
            EntityName = "ProjectEntity",
            EntityId = "proj_123",
            Operation = EntityOperationEnum.Insert,
            ChangesJson = null // Invalid
        };

        // Act
        var result = _validator.Validate(record);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(AuditRecordEntity.ChangesJson));
    }

    [Fact]
    public void Validate_EntityChangeMissingOperation_Fails()
    {
        // Arrange
        var record = new AuditRecordEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = Guid.NewGuid(),
            EventType = AuditEventTypeEnum.EntityChange,
            Severity = AuditSeverityEnum.Information,
            EntityName = "ProjectEntity",
            EntityId = "proj_123",
            Operation = null, // Invalid
            ChangesJson = "{}"
        };

        // Act
        var result = _validator.Validate(record);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(AuditRecordEntity.Operation));
    }

    [Fact]
    public void Validate_ValidSecurityEventWithNullEntityFields_Passes()
    {
        // Arrange - in security event, EntityName/ChangesJson are legitimately NULL
        var record = new AuditRecordEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = Guid.NewGuid(),
            EventType = AuditEventTypeEnum.Security,
            Severity = AuditSeverityEnum.Warning,
            Description = "Detected brute-force authentication attempt from IP 192.168.1.50",
            EntityName = null,
            EntityId = null,
            Operation = null,
            ChangesJson = null,
            IpAddress = "192.168.1.50",
            Created = DateTime.UtcNow,
            CreatedBy = "system"
        };

        // Act
        var result = _validator.Validate(record);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_SecurityEventMissingDescription_Fails()
    {
        // Arrange
        var record = new AuditRecordEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = Guid.NewGuid(),
            EventType = AuditEventTypeEnum.Security,
            Severity = AuditSeverityEnum.Warning,
            Description = null // Invalid for Security event
        };

        // Act
        var result = _validator.Validate(record);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(AuditRecordEntity.Description));
    }

    [Fact]
    public void Validate_MissingTenantId_Fails()
    {
        // Arrange
        var record = new AuditRecordEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = Guid.Empty, // Invalid
            EventType = AuditEventTypeEnum.TenantSwitch,
            Severity = AuditSeverityEnum.Information,
            Description = "Switched tenant context"
        };

        // Act
        var result = _validator.Validate(record);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(AuditRecordEntity.TenantId));
    }

    [Fact]
    public void Validate_UserTypeSpecifiedWithoutUserIdOrEmail_Fails()
    {
        // Arrange
        var record = new AuditRecordEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = Guid.NewGuid(),
            EventType = AuditEventTypeEnum.UserActivity,
            Severity = AuditSeverityEnum.Information,
            Description = "Profile updated",
            UserType = UserTypeEnum.ClientUser,
            UserId = null, // Invalid when UserType is set
            UserEmail = null // Invalid when UserType is set
        };

        // Act
        var result = _validator.Validate(record);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(AuditRecordEntity.UserId));
        result.Errors.Should().Contain(e => e.PropertyName == nameof(AuditRecordEntity.UserEmail));
    }

    [Theory]
    [InlineData("not-an-email", false)]
    [InlineData("user@domain", false)]
    [InlineData("user@domain.com", true)]
    [InlineData("system", true)]
    public void Validate_UserEmailFormat_ValidatesProperly(string email, bool expectedValid)
    {
        // Arrange
        var record = new AuditRecordEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = Guid.NewGuid(),
            EventType = AuditEventTypeEnum.UserActivity,
            Severity = AuditSeverityEnum.Information,
            Description = "User session heartbeat",
            UserEmail = email
        };

        // Act
        var result = _validator.Validate(record);

        // Assert
        if (expectedValid)
        {
            result.Errors.Should().NotContain(e => e.PropertyName == nameof(AuditRecordEntity.UserEmail));
        }
        else
        {
            result.Errors.Should().Contain(e => e.PropertyName == nameof(AuditRecordEntity.UserEmail));
        }
    }
}
