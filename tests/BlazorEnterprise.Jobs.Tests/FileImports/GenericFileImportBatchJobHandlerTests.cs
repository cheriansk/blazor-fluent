using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Security;
using BlazorEnterprise.Core.Storage;
using BlazorEnterprise.Jobs.FileImports;
using BlazorEnterprise.Jobs.FileImports.Configuration;
using BlazorEnterprise.Jobs.FileImports.Staging;
using BlazorEnterprise.Persistence.Context;
using BlazorEnterprise.Shared.Contracts.Engine.Client;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BlazorEnterprise.Jobs.Tests.FileImports;

public class GenericFileImportBatchJobHandlerTests
{
    private readonly Mock<ITenantEncryptionService> _encryptionServiceMock = new();
    private readonly Mock<ITenantBlobStorageService> _blobStorageServiceMock = new();
    private readonly Mock<INotifyService> _notifyServiceMock = new();
    private readonly Mock<IJobEventPublisher> _eventPublisherMock = new();
    private readonly Mock<IFileTemplateConfigRepository> _templateRepoMock = new();
    private readonly Mock<ILocalBatchStagerResolver> _stagerResolverMock = new();
    private readonly Mock<IEngineClient> _engineClientMock = new();
    private readonly Mock<ILogger<GenericFileImportBatchJobHandler>> _loggerMock = new();

    [Fact]
    public void Constructor_InitializesProperlyWithRequiredAndOptionalDependencies()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        var tenantContextMock = new Mock<ITenantContext>();
        tenantContextMock.Setup(t => t.TenantId).Returns(Guid.NewGuid());
        using var dbContext = new AppDbContext(options, new Mock<IDateTimeProvider>().Object, tenantContextMock.Object);

        // Act
        var handler = new GenericFileImportBatchJobHandler(
            dbContext,
            _encryptionServiceMock.Object,
            _blobStorageServiceMock.Object,
            _notifyServiceMock.Object,
            _eventPublisherMock.Object,
            _loggerMock.Object,
            _templateRepoMock.Object,
            _stagerResolverMock.Object,
            _engineClientMock.Object);

        // Assert
        handler.Should().NotBeNull();
    }
}
