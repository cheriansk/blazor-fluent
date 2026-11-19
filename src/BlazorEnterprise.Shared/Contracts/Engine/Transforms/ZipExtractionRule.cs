namespace BlazorEnterprise.Shared.Contracts.Engine.Transforms;

/// <summary>
/// Declarative rule governing how a ZIP archive entry is validated and extracted during Stage 1 staging.
/// </summary>
public sealed record ZipExtractionRule(
    string FileExtension,
    string? ExpectedSheetName,
    int MinFiles,
    int MaxFiles);
