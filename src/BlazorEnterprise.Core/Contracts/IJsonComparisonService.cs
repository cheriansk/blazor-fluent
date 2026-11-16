using BlazorEnterprise.Core.Dtos.DocumentParsing;

namespace BlazorEnterprise.Core.Contracts;

/// <summary>
/// Local RFC 6902 JSON Patch comparison service for comparing two parsed document JSON payloads.
/// </summary>
public interface IJsonComparisonService
{
    /// <summary>
    /// Compares the latest parsed JSON results for <paramref name="sourceFileId"/> and <paramref name="targetFileId"/>.
    /// </summary>
    Task<JsonComparisonResultDto> CompareJsonAsync(Guid sourceFileId, Guid targetFileId, CancellationToken ct = default);
}
