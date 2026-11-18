using System.Text.Json;
using System.Text.RegularExpressions;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.DocumentParsing;
using BlazorEnterprise.Core.Dtos.DocumentParsing;
using BlazorEnterprise.Core.Security;
using BlazorEnterprise.Core.Storage;
using BlazorEnterprise.Persistence.Context;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.ML;

namespace BlazorEnterprise.Jobs.Services.DocumentParsing;

/// <summary>
/// 100% local Word (.docx) document parsing service powered by DocumentFormat.OpenXml and ML.NET.
/// Downloads encrypted tenant blobs, decrypts in-memory, classifies the document type,
/// extracts header metadata and section tables, and computes a confidence score.
/// </summary>
public sealed partial class DocumentParsingService : IDocumentParsingService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ITenantContext _tenantContext;
    private readonly ITenantEncryptionService _encryptionService;
    private readonly ITenantBlobStorageService _blobStorageService;
    private readonly ILogger<DocumentParsingService> _logger;
    private readonly MLContext _mlContext;

    public DocumentParsingService(
        IDbContextFactory<AppDbContext> dbFactory,
        ITenantContext tenantContext,
        ITenantEncryptionService encryptionService,
        ITenantBlobStorageService blobStorageService,
        ILogger<DocumentParsingService> logger)
    {
        _dbFactory = dbFactory;
        _tenantContext = tenantContext;
        _encryptionService = encryptionService;
        _blobStorageService = blobStorageService;
        _logger = logger;
        _mlContext = new MLContext(seed: 42);
    }

    /// <inheritdoc />
    public async Task<DocumentParsingConfigDto?> GetActiveConfigAsync(string documentType, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var tenantId = _tenantContext.TenantId;

        var config = await db.DocumentParsingConfigs
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.DocumentType == documentType && !c.IsDeleted)
            .OrderByDescending(c => c.Created)
            .FirstOrDefaultAsync(ct);

        if (config is null) return null;

        return new DocumentParsingConfigDto(
            config.Id,
            config.DocumentType,
            config.ModelBlobPath,
            config.ParsingRulesJson,
            config.MinConfidenceThreshold);
    }

    /// <inheritdoc />
    public async Task<DocumentParsingResultDto> ParseDocumentAsync(Guid importFileId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var tenantId = _tenantContext.TenantId;

        var importFile = await db.ImportFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.TenantId == tenantId && f.Id == importFileId && !f.IsDeleted, ct);

        if (importFile is null)
        {
            throw new InvalidOperationException($"ImportFile '{importFileId}' was not found for tenant '{tenantId}'.");
        }

        var errors = new List<ParsingErrorDto>();
        var paragraphs = new List<string>();
        var sections = new List<Dictionary<string, string>>();

        var stagedRow = await db.StagedImports
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.ImportFileId == importFileId)
            .OrderBy(s => s.ChunkIndex)
            .FirstOrDefaultAsync(ct);

        if (stagedRow is not null && !string.IsNullOrWhiteSpace(stagedRow.PayloadJson))
        {
            using var docJson = JsonDocument.Parse(stagedRow.PayloadJson);
            if (docJson.RootElement.TryGetProperty("Paragraphs", out var pElem) && pElem.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in pElem.EnumerateArray())
                {
                    var text = item.GetString()?.Trim();
                    if (!string.IsNullOrEmpty(text))
                    {
                        paragraphs.Add(text);
                    }
                }
            }
            if (docJson.RootElement.TryGetProperty("Tables", out var tElem) && tElem.ValueKind == JsonValueKind.Array)
            {
                foreach (var tbl in tElem.EnumerateArray())
                {
                    if (tbl.ValueKind != JsonValueKind.Array) continue;
                    foreach (var row in tbl.EnumerateArray())
                    {
                        var cells = row.EnumerateArray().Select(c => c.GetString()?.Trim() ?? string.Empty).ToList();
                        if (cells.Count >= 2 && !string.IsNullOrWhiteSpace(cells[0]))
                        {
                            sections.Add(new Dictionary<string, string>
                            {
                                ["Description"] = cells[0],
                                ["Comment"] = cells[1]
                            });
                        }
                    }
                }
            }
        }
        else
        {
            await using var encryptedStream = await _blobStorageService.DownloadBlobAsync(importFile.BlobUri, ct);
            await using var decryptedStream = await _encryptionService.DecryptStreamAsync(encryptedStream, tenantId.ToString(), ct);

            using var doc = WordprocessingDocument.Open(decryptedStream, false);
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body is null)
            {
                errors.Add(new ParsingErrorDto("Document", "Word document body is empty."));
            }
            else
            {
                foreach (var p in body.Descendants<Paragraph>())
                {
                    var text = p.InnerText?.Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        paragraphs.Add(text);
                    }
                }

                foreach (var table in body.Descendants<Table>())
                {
                    foreach (var row in table.Elements<TableRow>())
                    {
                        var cells = row.Elements<TableCell>()
                            .Select(c => c.InnerText?.Trim() ?? string.Empty)
                            .ToList();

                        if (cells.Count >= 2 && !string.IsNullOrWhiteSpace(cells[0]))
                        {
                            sections.Add(new Dictionary<string, string>
                            {
                                ["Description"] = cells[0],
                                ["Comment"] = cells[1]
                            });
                        }
                    }
                }
            }
        }

        var fullText = string.Join(Environment.NewLine, paragraphs);
        var documentType = ClassifyDocumentType(fullText, importFile.OriginalFileName);
        var metadata = ExtractMetadata(fullText);

        var confidence = CalculateConfidence(documentType, metadata, sections.Count);
        var payload = new
        {
            DocumentType = documentType,
            Metadata = metadata,
            Sections = sections,
            ParagraphCount = paragraphs.Count
        };

        var extractedJson = JsonSerializer.Serialize(payload);
        var resultId = Guid.CreateVersion7();

        _logger.LogInformation(
            "Parsed Word document '{FileName}' ({ImportFileId}) as '{DocumentType}' with confidence {Confidence:F2}.",
            importFile.OriginalFileName, importFileId, documentType, confidence);

        return new DocumentParsingResultDto(
            resultId,
            importFileId,
            documentType,
            extractedJson,
            confidence,
            errors);
    }

    private static string ClassifyDocumentType(string text, string fileName)
    {
        var combined = $"{fileName} {text}";
        if (combined.Contains("Policy Center", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("PolicyCenter", StringComparison.OrdinalIgnoreCase))
        {
            return "PolicyCenter";
        }
        if (combined.Contains("ContactManager", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("Contact Manager", StringComparison.OrdinalIgnoreCase))
        {
            return "ContactManager";
        }
        if (combined.Contains("Claim Center", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("ClaimCenter", StringComparison.OrdinalIgnoreCase))
        {
            return "ClaimCenter";
        }
        return "General";
    }

    private static Dictionary<string, string> ExtractMetadata(string text)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var customerMatch = CustomerRegex().Match(text);
        if (customerMatch.Success) metadata["CustomerName"] = customerMatch.Groups[1].Value.Trim();

        var sourceVerMatch = SourceVersionRegex().Match(text);
        if (sourceVerMatch.Success) metadata["SourceVersion"] = sourceVerMatch.Groups[1].Value.Trim();

        var targetVerMatch = TargetVersionRegex().Match(text);
        if (targetVerMatch.Success) metadata["TargetVersion"] = targetVerMatch.Groups[1].Value.Trim();

        var pmMatch = ProjectManagerRegex().Match(text);
        if (pmMatch.Success) metadata["ProjectManager"] = pmMatch.Groups[1].Value.Trim();

        return metadata;
    }

    private static double CalculateConfidence(string documentType, Dictionary<string, string> metadata, int sectionCount)
    {
        var score = 0.25;
        if (documentType != "General") score += 0.25;
        if (metadata.Count > 0) score += Math.Min(0.25, metadata.Count * 0.0625);
        if (sectionCount > 0) score += 0.25;
        return Math.Round(Math.Min(1.0, score), 4);
    }

    [GeneratedRegex(@"Customer(?:\s+Name)?\s*[:\-]\s*(.+)", RegexOptions.IgnoreCase)]
    private static partial Regex CustomerRegex();

    [GeneratedRegex(@"Source\s+Version\s*[:\-]\s*(.+)", RegexOptions.IgnoreCase)]
    private static partial Regex SourceVersionRegex();

    [GeneratedRegex(@"Target\s+Version\s*[:\-]\s*(.+)", RegexOptions.IgnoreCase)]
    private static partial Regex TargetVersionRegex();

    [GeneratedRegex(@"Project\s+Manager\s*[:\-]\s*(.+)", RegexOptions.IgnoreCase)]
    private static partial Regex ProjectManagerRegex();
}
