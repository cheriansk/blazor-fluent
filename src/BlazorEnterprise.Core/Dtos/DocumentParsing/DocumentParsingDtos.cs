namespace BlazorEnterprise.Core.Dtos.DocumentParsing;

/// <summary>
/// Structured result returned by <see cref="BlazorEnterprise.Core.Contracts.IDocumentParsingService"/>.
/// </summary>
public sealed record DocumentParsingResultDto(
    Guid ResultId,
    Guid ImportFileId,
    string DocumentType,
    string ExtractedDataJson,
    double ConfidenceScore,
    IReadOnlyList<ParsingErrorDto> Errors);

/// <summary>
/// Per-tenant ML.NET document parsing configuration projection.
/// </summary>
public sealed record DocumentParsingConfigDto(
    Guid ConfigId,
    string DocumentType,
    string ModelBlobPath,
    string ParsingRulesJson,
    double MinConfidenceThreshold);

/// <summary>
/// Structured RFC 6902 JSON Patch comparison result returned by
/// <see cref="BlazorEnterprise.Core.Contracts.IJsonComparisonService"/>.
/// </summary>
public sealed record JsonComparisonResultDto(
    Guid ComparisonId,
    Guid SourceFileId,
    Guid TargetFileId,
    string DiffResultJson,
    DiffSummaryDto Summary);

/// <summary>
/// Summary counts of RFC 6902 JSON Patch operations between two parsed documents.
/// </summary>
public sealed record DiffSummaryDto(
    int Additions,
    int Deletions,
    int Modifications);

/// <summary>
/// Non-fatal parsing warning or section extraction error recorded during document parsing.
/// </summary>
public sealed record ParsingErrorDto(
    string Section,
    string Message);
