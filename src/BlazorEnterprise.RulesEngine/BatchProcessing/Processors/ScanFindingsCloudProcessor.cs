using BlazorEnterprise.Shared.Contracts.Engine.Transforms;
using BlazorEnterprise.Shared.Models;

namespace BlazorEnterprise.RulesEngine.BatchProcessing.Processors;

/// <summary>
/// Stage 2b Cloud Processor for <c>ScanFindingsWord</c> documents.
/// Validates extracted findings and projects them into <see cref="ProcessedDomainRowDto"/> items
/// targeting <c>app.ScanFindings</c>.
/// </summary>
public sealed class ScanFindingsCloudProcessor : BaseCloudProcessor
{
    public override IReadOnlyCollection<string> SupportedFileTypes { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ScanFindingsWord",
            "ScanFindings"
        };

    public override ProcessBatchRespDto ProcessChunk(
        Guid importId,
        Guid importFileId,
        int chunkIndex,
        string fileType,
        CanonicalStagedChunkPayload payload,
        IReadOnlyDictionary<string, string>? contextParameters)
    {
        var errors = new List<FileValidationError>();
        var outputRows = new List<ProcessedDomainRowDto>();
        var reverseHeaderMap = BuildReverseHeaderMap(payload.HeaderMap);

        var rows = payload.Rows ?? [];
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var rowNumber = row.Idx > 0 ? row.Idx : i + 1;

            var description = GetCellValue(row, reverseHeaderMap, "Description");
            if (string.IsNullOrWhiteSpace(description))
            {
                errors.Add(new FileValidationError(
                    payload.SheetName ?? "ScanFindings",
                    rowNumber,
                    "Description",
                    "Description is required for each scan finding."));
                continue;
            }

            var domainProps = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["DocumentType"] = fileType,
                ["CustomerName"] = GetCellValue(row, reverseHeaderMap, "CustomerName"),
                ["SourceVersion"] = GetCellValue(row, reverseHeaderMap, "SourceVersion"),
                ["TargetVersion"] = GetCellValue(row, reverseHeaderMap, "TargetVersion"),
                ["ProjectManager"] = GetCellValue(row, reverseHeaderMap, "ProjectManager"),
                ["Description"] = description.Trim(),
                ["Comment"] = GetCellValue(row, reverseHeaderMap, "Comment"),
                ["OrderIndex"] = rowNumber.ToString()
            };

            outputRows.Add(new ProcessedDomainRowDto(
                row.Id,
                rowNumber,
                "ScanFinding",
                null,
                false,
                null,
                domainProps));
        }

        if (errors.Count > 0)
        {
            return new ProcessBatchRespDto(
                TenantId: payload.TenantId,
                ImportFileId: importFileId,
                ChunkIndex: chunkIndex,
                IsSuccess: false,
                IsEncryptedAtRest: false,
                ProcessedRows: [],
                Errors: errors,
                ErrorMessage: $"ScanFindings validation failed with {errors.Count} error(s).");
        }

        return new ProcessBatchRespDto(
            TenantId: payload.TenantId,
            ImportFileId: importFileId,
            ChunkIndex: chunkIndex,
            IsSuccess: true,
            IsEncryptedAtRest: false,
            ProcessedRows: outputRows,
            Errors: [],
            ErrorMessage: null);
    }
}
