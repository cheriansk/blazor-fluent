using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BlazorEnterprise.Core.Tests.Api;
using BlazorEnterprise.RulesEngine.BatchProcessing;
using BlazorEnterprise.RulesEngine.BatchProcessing.Processors;
using BlazorEnterprise.Shared.Common;
using BlazorEnterprise.Shared.Contracts.Engine.Client;
using BlazorEnterprise.Shared.Contracts.Engine.Transforms;
using BlazorEnterprise.Shared.Security;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BlazorEnterprise.Core.Tests.BatchProcessing;

public class HybridBatchProcessingTests : IClassFixture<ApiTestWebApplicationFactory>
{
    private readonly ApiTestWebApplicationFactory _factory;

    public HybridBatchProcessingTests(ApiTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void TaskCloudProcessor_ValidChunkWithDynamicHeaderMap_TransformsAllRows()
    {
        var processor = new TaskCloudProcessor();
        var dispatcher = new CloudProcessorDispatcher([processor]);

        var importId = Guid.CreateVersion7();
        var importFileId = Guid.CreateVersion7();
        var row1Id = Guid.CreateVersion7();
        var row2Id = Guid.CreateVersion7();

        var headerMap = new Dictionary<string, string>
        {
            ["c1"] = "Title",
            ["c2"] = "Description",
            ["c3"] = "Status",
            ["c4"] = "Priority",
            ["c5"] = "DueDate",
            ["c6"] = "StoryPoints"
        };

        var rows = new List<StagedRowDto>
        {
            new(row1Id, 1, new Dictionary<string, string?>
            {
                ["c1"] = "Implement Canonical Stager",
                ["c2"] = "Build SAX streaming parser",
                ["c3"] = "In Progress",
                ["c4"] = "High",
                ["c5"] = "2026-11-01",
                ["c6"] = "8"
            }),
            new(row2Id, 2, new Dictionary<string, string?>
            {
                ["c1"] = "Verify Vault Encryption"
            })
        };

        var tenantId = Guid.Parse(ApiTestWebApplicationFactory.ValidTenantId);
        var payload = new CanonicalStagedChunkPayload(
            TenantId: tenantId,
            ImportId: importId,
            ImportFileId: importFileId,
            FileType: "TaskCsv",
            SheetName: "Default",
            ChunkIndex: 0,
            TotalChunks: 1,
            RowCount: 2,
            IsLastChunk: true,
            HeaderMap: headerMap,
            Rows: rows);

        var resp = dispatcher.Dispatch(
            importId: importId,
            importFileId: importFileId,
            chunkIndex: 0,
            fileType: "TaskCsv",
            payload: payload,
            contextParameters: null);

        resp.IsSuccess.Should().BeTrue();
        resp.Errors.Should().BeEmpty();
        resp.ProcessedRows.Should().HaveCount(2);

        resp.ProcessedRows[0].SourceRowId.Should().Be(row1Id);
        resp.ProcessedRows[0].DomainProperties["Title"].Should().Be("Implement Canonical Stager");
        resp.ProcessedRows[0].DomainProperties["Status"].Should().Be("InProgress");
        resp.ProcessedRows[0].DomainProperties["Priority"].Should().Be("High");

        resp.ProcessedRows[1].SourceRowId.Should().Be(row2Id);
        resp.ProcessedRows[1].DomainProperties["Title"].Should().Be("Verify Vault Encryption");
        resp.ProcessedRows[1].DomainProperties["Status"].Should().Be("Open");
        resp.ProcessedRows[1].DomainProperties["Priority"].Should().Be("Medium");
    }

    [Fact]
    public void TaskCloudProcessor_AllOrNothingAtomicity_RejectsEntireBatchWhenSingleRowFails()
    {
        var processor = new TaskCloudProcessor();
        var importId = Guid.CreateVersion7();
        var importFileId = Guid.CreateVersion7();

        var headerMap = new Dictionary<string, string>
        {
            ["c1"] = "Title",
            ["c2"] = "Priority"
        };

        var rows = new List<StagedRowDto>
        {
            new(Guid.CreateVersion7(), 1, new Dictionary<string, string?>
            {
                ["c1"] = "Valid Task 1",
                ["c2"] = "High"
            }),
            new(Guid.CreateVersion7(), 2, new Dictionary<string, string?>
            {
                ["c1"] = "Invalid Priority Task",
                ["c2"] = "INVALID_PRIORITY_VALUE"
            })
        };

        var tenantId = Guid.Parse(ApiTestWebApplicationFactory.ValidTenantId);
        var payload = new CanonicalStagedChunkPayload(
            TenantId: tenantId,
            ImportId: importId,
            ImportFileId: importFileId,
            FileType: "TaskExcel",
            SheetName: "Tasks",
            ChunkIndex: 0,
            TotalChunks: 1,
            RowCount: 2,
            IsLastChunk: true,
            HeaderMap: headerMap,
            Rows: rows);

        var resp = processor.ProcessChunk(
            importId,
            importFileId,
            0,
            "TaskExcel",
            payload,
            null);

        resp.IsSuccess.Should().BeFalse();
        resp.ProcessedRows.Should().BeEmpty("All-or-Nothing atomicity guarantees zero rows committed when any row fails");
        resp.Errors.Should().ContainSingle(e => e.RowIndex == 2 && e.ColumnName == "Priority");
    }

    [Fact]
    public void PayloadEncryptionUtility_PackedBytesAndGzip_RoundTripsCleanly()
    {
        var vaultKey = PayloadEncryptionUtility.DeriveVaultAtRestKey("MasterSecretKey2026!", "TENANT-001");
        var originalProps = new Dictionary<string, string?>
        {
            ["Title"] = "Confidential Strategic Initiative",
            ["Description"] = "Proprietary algorithm details"
        };

        var packedBytes = PayloadEncryptionUtility.EncryptToPackedBytes(originalProps, vaultKey);
        packedBytes.Length.Should().BeGreaterThan(28); // 12-byte Nonce + 16-byte Tag + Ciphertext

        var decryptedProps = PayloadEncryptionUtility.DecryptFromPackedBytes<Dictionary<string, string?>>(packedBytes, vaultKey);
        decryptedProps.Should().NotBeNull();
        decryptedProps!["Title"].Should().Be("Confidential Strategic Initiative");
        decryptedProps["Description"].Should().Be("Proprietary algorithm details");

        var rawJson = "{\"sheetName\":\"Default\",\"rows\":[1,2,3]}";
        var compressed = PayloadEncryptionUtility.CompressGzip(rawJson);
        var decompressed = PayloadEncryptionUtility.DecompressGzip(compressed);
        decompressed.Should().Be(rawJson);
    }

    [Fact]
    public async Task ProcessBatchAndDecryptProprietary_WithAtRestEncryptionEnabled_RedactsAndDecryptsOnDemand()
    {
        using var encryptedFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ProprietaryStorage:EncryptProcessedDataAtRest"] = "true",
                    ["ProprietaryStorage:VaultMasterKey"] = "Test-Vault-Master-Key-2026-Proprietary!"
                });
            });
        });

        using var client = encryptedFactory.CreateClient();
        var token = _factory.GenerateTestToken("user@company.com", ApiTestWebApplicationFactory.ValidTenantId);
        var transitKey = PayloadEncryptionUtility.DeriveTenantKey(
            ApiTestWebApplicationFactory.ValidLicenseKey,
            ApiTestWebApplicationFactory.ValidTenantId);

        var importId = Guid.CreateVersion7();
        var importFileId = Guid.CreateVersion7();
        var rowId = Guid.CreateVersion7();

        var headerMap = new Dictionary<string, string>
        {
            ["c1"] = "Title",
            ["c2"] = "Description",
            ["c3"] = "Priority"
        };

        var rows = new List<StagedRowDto>
        {
            new(rowId, 1, new Dictionary<string, string?>
            {
                ["c1"] = "Top Secret Merger Task",
                ["c2"] = "Confidential M&A due diligence",
                ["c3"] = "Urgent"
            })
        };

        var tenantId = Guid.Parse(ApiTestWebApplicationFactory.ValidTenantId);
        var stagedPayload = new CanonicalStagedChunkPayload(
            TenantId: tenantId,
            ImportId: importId,
            ImportFileId: importFileId,
            FileType: "TaskCsv",
            SheetName: "Default",
            ChunkIndex: 0,
            TotalChunks: 1,
            RowCount: 1,
            IsLastChunk: true,
            HeaderMap: headerMap,
            Rows: rows);

        var compressedBytes = PayloadEncryptionUtility.CompressGzip(JsonDefaults.ToJson(stagedPayload));
        var processReq = new ProcessBatchReqDto(
            TenantId: tenantId,
            ImportId: importId,
            ImportFileId: importFileId,
            FileType: "TaskCsv",
            ChunkIndex: 0,
            TotalChunks: 1,
            IsLastChunk: true,
            HeaderMap: headerMap,
            Rows: rows,
            CompressedChunkBytes: compressedBytes);

        using var httpReq = new HttpRequestMessage(HttpMethod.Post, EngineRoutes.TransformsProcessBatch);
        httpReq.Headers.Add(EngineHeaderConstants.CorrelationId, "CORR-BATCH-001");
        httpReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        httpReq.Content = JsonContent.Create(PayloadEncryptionUtility.EncryptPayload(processReq, transitKey));

        var httpResp = await client.SendAsync(httpReq);
        httpResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var respEnvelope = await httpResp.Content.ReadFromJsonAsync<EncryptedEnvelopeDto>();
        var batchResp = PayloadEncryptionUtility.DecryptPayload<ProcessBatchRespDto>(respEnvelope!, transitKey);

        batchResp.Should().NotBeNull();
        batchResp!.IsSuccess.Should().BeTrue();
        batchResp.ProcessedRows.Should().ContainSingle();

        var processedRow = batchResp.ProcessedRows[0];
        processedRow.IsEncryptedAtRest.Should().BeTrue();
        processedRow.EncryptedPayloadBytes.Should().NotBeNullOrEmpty();
        processedRow.DomainProperties["Title"].Should().Be("[ENCRYPTED]");
        processedRow.DomainProperties["Description"].Should().Be("[ENCRYPTED]");

        // Now verify on-demand decryption via /api/v1/transforms/decrypt-proprietary
        var recordId = Guid.CreateVersion7();
        var decryptReq = new DecryptProprietaryReqDto(
        [
            new DecryptProprietaryItemDto(recordId, processedRow.EncryptedPayloadBytes!)
        ]);

        using var decryptHttpReq = new HttpRequestMessage(HttpMethod.Post, EngineRoutes.TransformsDecryptProprietary);
        decryptHttpReq.Headers.Add(EngineHeaderConstants.CorrelationId, "CORR-DECRYPT-001");
        decryptHttpReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        decryptHttpReq.Content = JsonContent.Create(PayloadEncryptionUtility.EncryptPayload(decryptReq, transitKey));

        var decryptHttpResp = await client.SendAsync(decryptHttpReq);
        decryptHttpResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var decryptEnvelope = await decryptHttpResp.Content.ReadFromJsonAsync<EncryptedEnvelopeDto>();
        var decryptResp = PayloadEncryptionUtility.DecryptPayload<DecryptProprietaryRespDto>(decryptEnvelope!, transitKey);

        decryptResp.Should().NotBeNull();
        decryptResp!.Items.Should().ContainSingle();
        decryptResp.Items[0].RecordId.Should().Be(recordId);
        decryptResp.Items[0].Properties["Title"].Should().Be("Top Secret Merger Task");
        decryptResp.Items[0].Properties["Description"].Should().Be("Confidential M&A due diligence");
    }

    [Fact]
    public void CalendarCloudProcessor_ValidTimelineAndMilestone_TransformsAndLinksParentTimeline()
    {
        var processor = new CalendarCloudProcessor();
        var importId = Guid.CreateVersion7();
        var importFileId = Guid.CreateVersion7();
        var tenantId = Guid.Parse(ApiTestWebApplicationFactory.ValidTenantId);

        var timelineRowId = Guid.CreateVersion7();
        var milestoneRowId = Guid.CreateVersion7();

        var headerMap = new Dictionary<string, string>
        {
            ["c1"] = "EntryType",
            ["c2"] = "Title",
            ["c3"] = "StartDate",
            ["c4"] = "EndDate",
            ["c5"] = "Status",
            ["c6"] = "OwnerEmails",
            ["c7"] = "Comments"
        };

        var rows = new List<StagedRowDto>
        {
            new(timelineRowId, 1, new Dictionary<string, string?>
            {
                ["c1"] = "Timeline",
                ["c2"] = "Phase 1: Architecture Inception",
                ["c3"] = "2026-11-01",
                ["c4"] = "2026-12-01",
                ["c5"] = "InProgress",
                ["c7"] = "Core system stage"
            }),
            new(milestoneRowId, 2, new Dictionary<string, string?>
            {
                ["c1"] = "Milestone",
                ["c2"] = "Gate 1 Review",
                ["c3"] = "2026-11-15",
                ["c4"] = "2026-11-15",
                ["c5"] = "Scheduled",
                ["c6"] = "lead@enterprise.com; reviewer@enterprise.com",
                ["c7"] = "Sign-off"
            })
        };

        var payload = new CanonicalStagedChunkPayload(
            TenantId: tenantId,
            ImportId: importId,
            ImportFileId: importFileId,
            FileType: "ProjectCalendarExcel",
            SheetName: "Calendar",
            ChunkIndex: 0,
            TotalChunks: 1,
            RowCount: 2,
            IsLastChunk: true,
            HeaderMap: headerMap,
            Rows: rows);

        var resp = processor.ProcessChunk(
            importId,
            importFileId,
            0,
            "ProjectCalendarExcel",
            payload,
            null);

        resp.IsSuccess.Should().BeTrue();
        resp.Errors.Should().BeEmpty();
        resp.ProcessedRows.Should().HaveCount(2);

        var timelineRow = resp.ProcessedRows[0];
        timelineRow.DomainProperties["EntryType"].Should().Be("Timeline");
        timelineRow.DomainProperties["Title"].Should().Be("Phase 1: Architecture Inception");
        timelineRow.DomainProperties["ParentTimelineSourceRowId"].Should().BeNull();

        var milestoneRow = resp.ProcessedRows[1];
        milestoneRow.DomainProperties["EntryType"].Should().Be("Milestone");
        milestoneRow.DomainProperties["Title"].Should().Be("Gate 1 Review");
        milestoneRow.DomainProperties["ParentTimelineSourceRowId"].Should().Be(timelineRowId.ToString());
        milestoneRow.DomainProperties["ParentTimelineTitle"].Should().Be("Phase 1: Architecture Inception");
    }

    [Fact]
    public void CalendarCloudProcessor_AllOrNothing_RejectsWhenMilestonePrecedesTimelineOrHasBadEmail()
    {
        var processor = new CalendarCloudProcessor();
        var importId = Guid.CreateVersion7();
        var importFileId = Guid.CreateVersion7();
        var tenantId = Guid.Parse(ApiTestWebApplicationFactory.ValidTenantId);

        var headerMap = new Dictionary<string, string>
        {
            ["c1"] = "EntryType",
            ["c2"] = "Title",
            ["c3"] = "StartDate",
            ["c4"] = "EndDate",
            ["c5"] = "OwnerEmails"
        };

        var rows = new List<StagedRowDto>
        {
            new(Guid.CreateVersion7(), 1, new Dictionary<string, string?>
            {
                ["c1"] = "Milestone",
                ["c2"] = "Orphan Milestone Without Timeline",
                ["c3"] = "2026-11-15",
                ["c4"] = "2026-11-15",
                ["c5"] = "not-an-email"
            })
        };

        var payload = new CanonicalStagedChunkPayload(
            TenantId: tenantId,
            ImportId: importId,
            ImportFileId: importFileId,
            FileType: "ProgramCalendarExcel",
            SheetName: "Calendar",
            ChunkIndex: 0,
            TotalChunks: 1,
            RowCount: 1,
            IsLastChunk: true,
            HeaderMap: headerMap,
            Rows: rows);

        var resp = processor.ProcessChunk(
            importId,
            importFileId,
            0,
            "ProgramCalendarExcel",
            payload,
            null);

        resp.IsSuccess.Should().BeFalse();
        resp.ProcessedRows.Should().BeEmpty();
        resp.Errors.Should().HaveCountGreaterThanOrEqualTo(2);
        resp.Errors.Should().Contain(e => e.ColumnName == "EntryType" && e.ErrorMessage.Contains("preceded by a parent Timeline"));
        resp.Errors.Should().Contain(e => e.ColumnName == "OwnerEmails" && e.ErrorMessage.Contains("not a valid email address"));
    }
}
