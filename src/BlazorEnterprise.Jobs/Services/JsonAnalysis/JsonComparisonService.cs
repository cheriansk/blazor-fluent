using System.Text.Json;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Dtos.DocumentParsing;
using BlazorEnterprise.Persistence.Context;
using JsonDiffPatchDotNet;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace BlazorEnterprise.Jobs.Services.JsonAnalysis;

/// <summary>
/// Compares two parsed <see cref="Core.Domain.DocumentParsing.DocumentParsingResultEntity"/> JSON payloads
/// using <see cref="JsonDiffPatch"/> to produce an RFC 6902-style diff and summary counts.
/// </summary>
public sealed class JsonComparisonService : IJsonComparisonService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<JsonComparisonService> _logger;

    public JsonComparisonService(
        IDbContextFactory<AppDbContext> dbFactory,
        ITenantContext tenantContext,
        ILogger<JsonComparisonService> logger)
    {
        _dbFactory = dbFactory;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<JsonComparisonResultDto> CompareJsonAsync(Guid sourceFileId, Guid targetFileId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var tenantId = _tenantContext.TenantId;

        var sourceResult = await db.DocumentParsingResults
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.ImportFileId == sourceFileId && !r.IsDeleted)
            .OrderByDescending(r => r.Created)
            .FirstOrDefaultAsync(ct);

        var targetResult = await db.DocumentParsingResults
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.ImportFileId == targetFileId && !r.IsDeleted)
            .OrderByDescending(r => r.Created)
            .FirstOrDefaultAsync(ct);

        if (sourceResult is null || targetResult is null)
        {
            throw new InvalidOperationException("Both source and target files must be parsed before running JSON comparison.");
        }

        var jdp = new JsonDiffPatch();
        var left = JToken.Parse(sourceResult.ExtractedDataJson);
        var right = JToken.Parse(targetResult.ExtractedDataJson);
        var diffToken = jdp.Diff(left, right);

        var diffJson = diffToken?.ToString(Newtonsoft.Json.Formatting.None) ?? "{}";
        var (additions, deletions, modifications) = CountDiffOperations(diffToken);

        _logger.LogInformation(
            "Compared parsed JSON for source '{SourceFileId}' vs target '{TargetFileId}': +{Additions}, -{Deletions}, ~{Modifications}.",
            sourceFileId, targetFileId, additions, deletions, modifications);

        return new JsonComparisonResultDto(
            Guid.CreateVersion7(),
            sourceFileId,
            targetFileId,
            diffJson,
            new DiffSummaryDto(additions, deletions, modifications));
    }

    private static (int Additions, int Deletions, int Modifications) CountDiffOperations(JToken? token)
    {
        if (token is null) return (0, 0, 0);

        int additions = 0, deletions = 0, modifications = 0;

        if (token is JArray arr)
        {
            if (arr.Count == 1) additions++;
            else if (arr.Count == 2) modifications++;
            else if (arr.Count == 3 && arr[2].Type == JTokenType.Integer && arr[2].Value<int>() == 0) deletions++;
        }
        else if (token is JObject obj)
        {
            foreach (var prop in obj.Properties())
            {
                if (prop.Name == "_t") continue;
                var (a, d, m) = CountDiffOperations(prop.Value);
                additions += a;
                deletions += d;
                modifications += m;
            }
        }

        return (additions, deletions, modifications);
    }
}
