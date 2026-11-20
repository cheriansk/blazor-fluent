using System.Text.Json;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Imports;
using BlazorEnterprise.Core.Security;
using BlazorEnterprise.Core.Storage;
using BlazorEnterprise.Jobs.Services.DocumentParsing;
using BlazorEnterprise.Persistence.Context;
using BlazorEnterprise.Shared.DataListTypes;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace BlazorEnterprise.Jobs.Tests.Services;

public class DocumentParsingServiceTests
{
    [Fact]
    public async Task ParseDocumentAsync_ExtractsMetadataAndSections_FromValidDocx()
    {
        // Arrange: create an in-memory .docx with metadata paragraphs and a 2-column section table
        await using var docxStream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(docxStream, WordprocessingDocumentType.Document, true))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body(
                new Paragraph(new Run(new Text("PolicyCenter Upgrade Assessment"))),
                new Paragraph(new Run(new Text("Customer Name: Acme Insurance"))),
                new Paragraph(new Run(new Text("Source Version: 10.0"))),
                new Paragraph(new Run(new Text("Target Version: 2025.04"))),
                new Paragraph(new Run(new Text("Project Manager: Jane Doe"))),
                new Table(
                    new TableRow(
                        new TableCell(new Paragraph(new Run(new Text("Policy Line Configuration")))),
                        new TableCell(new Paragraph(new Run(new Text("Migrate custom coverages")))))
                )));
            mainPart.Document.Save();
        }
        docxStream.Position = 0;

        var tenantId = Guid.CreateVersion7();
        var fileId = Guid.CreateVersion7();

        using var sqliteConn = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        await sqliteConn.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(sqliteConn)
            .Options;

        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(d => d.Now).Returns(DateTime.UtcNow);

        var tenantContext = new Mock<ITenantContext>();
        tenantContext.Setup(t => t.TenantId).Returns(tenantId);
        tenantContext.Setup(t => t.HasActiveTenant).Returns(true);

        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.IsAuthenticated).Returns(true);
        currentUser.Setup(u => u.UserId).Returns("test-user");

        await using (var seedDb = new AppDbContext(options, dateTimeProvider.Object, tenantContext.Object, currentUser.Object))
        {
            await seedDb.Database.EnsureCreatedAsync();
            seedDb.ImportFiles.Add(new ImportFileEntity
            {
                Id = fileId,
                TenantId = tenantId,
                ImportId = Guid.CreateVersion7(),
                StorageFolder = "tenants/test",
                StepStage = "Uploaded",
                FileName = "policy.docx",
                OriginalFileName = "PolicyCenter_Assessment.docx",
                BlobUri = "tenants/test/policy.bin",
                ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                FileType = ImportFileTypeEnum.ScanFindings,
                Status = ImportStatusEnum.Uploaded,
                Sha256Checksum = "abc"
            });
            await seedDb.SaveChangesAsync();
        }

        var dbFactory = new Mock<IDbContextFactory<AppDbContext>>();
        dbFactory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new AppDbContext(options, dateTimeProvider.Object, tenantContext.Object, currentUser.Object));

        var blobStorage = new Mock<ITenantBlobStorageService>();
        blobStorage.Setup(b => b.DownloadBlobAsync("tenants/test/policy.bin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(docxStream);

        var encryption = new Mock<ITenantEncryptionService>();
        encryption.Setup(e => e.DecryptStreamAsync(It.IsAny<Stream>(), tenantId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream s, string _, CancellationToken _) => s);

        var sut = new DocumentParsingService(
            dbFactory.Object,
            tenantContext.Object,
            encryption.Object,
            blobStorage.Object,
            NullLogger<DocumentParsingService>.Instance);

        // Act
        var result = await sut.ParseDocumentAsync(fileId);

        // Assert
        result.DocumentType.Should().Be("PolicyCenter");
        result.ConfidenceScore.Should().BeGreaterThanOrEqualTo(0.9);
        result.Errors.Should().BeEmpty();

        using var parsed = JsonDocument.Parse(result.ExtractedDataJson);
        parsed.RootElement.GetProperty("Metadata").GetProperty("CustomerName").GetString().Should().Be("Acme Insurance");
        parsed.RootElement.GetProperty("Sections").GetArrayLength().Should().Be(1);
    }
}
