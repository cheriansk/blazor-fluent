using BlazorEnterprise.Core.Domain.Notifications;
using BlazorEnterprise.Infrastructure.Notifications;
using BlazorEnterprise.Shared.DataListTypes;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BlazorEnterprise.Infrastructure.Tests.Notifications;

public class EmailNotificationSenderTests
{
    private readonly Mock<ILogger<EmailNotificationSender>> _loggerMock = new();

    [Fact]
    public async Task SendEmailNotificationAsync_WhenRecipientIsEmpty_ReturnsFalse()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["Notifications:Email:Enabled"] = "true",
            ["Notifications:Email:SmtpHost"] = "smtp.example.com",
            ["Notifications:Email:SenderEmail"] = "noreply@example.com"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        var sender = new EmailNotificationSender(configuration, _loggerMock.Object);
        var notification = new NotificationEntity
        {
            Id = Guid.NewGuid(),
            Category = NotificationCategoryEnum.Generic,
            Title = "Test Notification",
            Message = "Test message content",
            Severity = NotificationSeverityEnum.Info
        };

        // Act
        var result = await sender.SendEmailNotificationAsync(notification, recipientMailbox: string.Empty);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SendEmailNotificationAsync_WhenDisabledInConfiguration_ReturnsFalse()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["Notifications:Email:Enabled"] = "false",
            ["Notifications:Email:SmtpHost"] = "smtp.example.com"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        var sender = new EmailNotificationSender(configuration, _loggerMock.Object);
        var notification = new NotificationEntity
        {
            Id = Guid.NewGuid(),
            Category = NotificationCategoryEnum.Generic,
            Title = "Alert",
            Message = "Server maintenance",
            Severity = NotificationSeverityEnum.Warning
        };

        // Act
        var result = await sender.SendEmailNotificationAsync(notification, "user@example.com");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SendEmailNotificationAsync_WhenSmtpHostMissing_ReturnsFalse()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["Notifications:Email:Enabled"] = "true",
            ["Notifications:Email:SmtpHost"] = ""
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        var sender = new EmailNotificationSender(configuration, _loggerMock.Object);
        var notification = new NotificationEntity
        {
            Id = Guid.NewGuid(),
            Category = NotificationCategoryEnum.Generic,
            Title = "Update",
            Message = "New task assigned",
            Severity = NotificationSeverityEnum.Info
        };

        // Act
        var result = await sender.SendEmailNotificationAsync(notification, "user@example.com");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SendEmailNotificationAsync_WhenSenderEmailMissing_ReturnsFalse()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["Notifications:Email:Enabled"] = "true",
            ["Notifications:Email:SmtpHost"] = "smtp.example.com",
            ["Notifications:Email:SenderEmail"] = ""
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        var sender = new EmailNotificationSender(configuration, _loggerMock.Object);
        var notification = new NotificationEntity
        {
            Id = Guid.NewGuid(),
            Category = NotificationCategoryEnum.Generic,
            Title = "Update",
            Message = "Security alert",
            Severity = NotificationSeverityEnum.Error
        };

        // Act
        var result = await sender.SendEmailNotificationAsync(notification, "user@example.com");

        // Assert
        result.Should().BeFalse();
    }
}
