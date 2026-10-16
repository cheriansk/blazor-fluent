using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BlazorFluent.Core.Tests.Api;
using BlazorFluent.RulesEngine.BatchProcessing;
using BlazorFluent.RulesEngine.BatchProcessing.Processors;
using BlazorFluent.Shared.Common;
using BlazorFluent.Shared.Contracts.Engine.Client;
using BlazorFluent.Shared.Contracts.Engine.Transforms;
using BlazorFluent.Shared.Security;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BlazorFluent.Core.Tests.BatchProcessing;

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

        var payload = new CanonicalStagedChunkPayload(
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

        var payload = new CanonicalStagedChunkPayload(
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

        var stagedPayload = new CanonicalStagedChunkPayload(
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
}
