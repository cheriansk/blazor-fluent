using System.Threading.Channels;
using BlazorFluent.Core.Configuration;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.Events;
using BlazorFluent.Jobs.Abstractions;
using BlazorFluent.Jobs.Listeners;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace BlazorFluent.Jobs.Tests.Listeners;

public class BatchJobQueueListenerTests
{
    private readonly Mock<IJobEventQueue> _queueMock = new();
    private readonly Mock<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory> _scopeFactoryMock = new();
    private readonly Mock<IHostApplicationLifetime> _lifetimeMock = new();
    private readonly Mock<IOptions<JobsOptions>> _optionsMock = new();
    private readonly Mock<ILogger<BatchJobQueueListener>> _loggerMock = new();

    public BatchJobQueueListenerTests()
    {
        _lifetimeMock.Setup(l => l.ApplicationStopping).Returns(new CancellationTokenSource().Token);
        _optionsMock.Setup(o => o.Value).Returns(new JobsOptions
        {
            CronServiceAccountEmail = "cron-daemon@example.com"
        });
    }

    private class TestJobEvent : IJobEvent
    {
        public Guid EventId { get; set; } = Guid.NewGuid();
        public Guid TenantId { get; set; }
        public string? CorrelationId { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public Guid? ParentExecutionId { get; set; }
        public string TriggerSource { get; set; } = "Cron";
        public string? SenderUserId { get; set; }
        public string? SenderUserEmail { get; set; }
        public string? SenderOrigin { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    [Fact]
    public void Constructor_RegistersApplicationStoppingCallback()
    {
        // Act
        var listener = new BatchJobQueueListener(
            _queueMock.Object,
            _scopeFactoryMock.Object,
            _lifetimeMock.Object,
            _optionsMock.Object,
            _loggerMock.Object);

        // Assert
        listener.Should().NotBeNull();
        _lifetimeMock.Verify(l => l.ApplicationStopping, Times.Once);
    }

    [Fact]
    public async Task ProcessJobEventAsync_WhenTenantIdIsEmpty_AbortsFailClosedWithoutCreatingScope()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var invalidEvent = new TestJobEvent { TenantId = Guid.Empty };

        var channel = Channel.CreateUnbounded<IJobEvent>();
        await channel.Writer.WriteAsync(invalidEvent);
        channel.Writer.Complete();

        _queueMock.Setup(q => q.ReadAllAsync(It.IsAny<CancellationToken>()))
            .Returns(channel.Reader.ReadAllAsync(cts.Token));

        var listener = new BatchJobQueueListener(
            _queueMock.Object,
            _scopeFactoryMock.Object,
            _lifetimeMock.Object,
            _optionsMock.Object,
            _loggerMock.Object);

        // Act
        await listener.StartAsync(cts.Token);
        await Task.Delay(100);
        await listener.StopAsync(cts.Token);

        // Assert: Scope must not be created because TenantId is empty (Zero-Trust Fail-Closed)
        _scopeFactoryMock.Verify(s => s.CreateScope(), Times.Never);
    }
}
