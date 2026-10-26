using System.Net;
using System.Text.Json;
using BlazorEnterprise.Core.Domain.Notifications;
using BlazorEnterprise.Infrastructure.Notifications;
using BlazorEnterprise.Shared.DataListTypes;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace BlazorEnterprise.Infrastructure.Tests.Notifications;

public class TeamsNotificationSenderTests
{
    private readonly Mock<ILogger<TeamsNotificationSender>> _loggerMock = new();
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock = new();

    [Fact]
    public async Task SendTeamsNotificationAsync_WhenWebhookUrlIsEmpty_ReturnsFalse()
    {
        // Arrange
        var sender = new TeamsNotificationSender(_httpClientFactoryMock.Object, _loggerMock.Object);
        var notification = new NotificationEntity
        {
            Id = Guid.NewGuid(),
            Category = NotificationCategoryEnum.Generic,
            Title = "Alert",
            Message = "Test message",
            Severity = NotificationSeverityEnum.Info
        };

        // Act
        var result = await sender.SendTeamsNotificationAsync(notification, webhookUrl: string.Empty);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SendTeamsNotificationAsync_WhenValidWebhook_DispatchesCardAndReturnsTrue()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("1")
            });

        var httpClient = new HttpClient(handlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var sender = new TeamsNotificationSender(_httpClientFactoryMock.Object, _loggerMock.Object);
        var notification = new NotificationEntity
        {
            Id = Guid.NewGuid(),
            Category = NotificationCategoryEnum.Generic,
            Title = "Deployment Successful",
            Message = "Production build was deployed successfully.",
            Severity = NotificationSeverityEnum.Success,
            LinkUrl = "https://app.example.com/deployments/123"
        };

        // Act
        var result = await sender.SendTeamsNotificationAsync(notification, "https://outlook.office.com/webhook/test-webhook");

        // Assert
        result.Should().BeTrue();
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post &&
                req.RequestUri!.ToString().Contains("outlook.office.com")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendTeamsNotificationAsync_WhenHttpFails_ReturnsFalse()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError
            });

        var httpClient = new HttpClient(handlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var sender = new TeamsNotificationSender(_httpClientFactoryMock.Object, _loggerMock.Object);
        var notification = new NotificationEntity
        {
            Id = Guid.NewGuid(),
            Category = NotificationCategoryEnum.Generic,
            Title = "Failure",
            Message = "Job failed",
            Severity = NotificationSeverityEnum.Error
        };

        // Act
        var result = await sender.SendTeamsNotificationAsync(notification, "https://outlook.office.com/webhook/failing-endpoint");

        // Assert
        result.Should().BeFalse();
    }
}
