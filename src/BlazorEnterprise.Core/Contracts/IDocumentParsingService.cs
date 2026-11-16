using BlazorEnterprise.Core.Dtos.DocumentParsing;

namespace BlazorEnterprise.Core.Contracts;

/// <summary>
/// Local ML.NET + OpenXML Word (.docx) document parsing service.
/// Downloads encrypted tenant blobs, decrypts them in-memory, classifies the document type,
/// extracts header metadata and section tables, and persists structured JSON results.
/// </summary>
public interface IDocumentParsingService
{
    /// <summary>
    /// Parses an uploaded Word (.docx) <see cref="Domain.Imports.ImportFileEntity"/> into structured JSON.
    /// </summary>
    Task<DocumentParsingResultDto> ParseDocumentAsync(Guid importFileId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the active per-tenant ML.NET parsing configuration for <paramref name="documentType"/>, if any.
    /// </summary>
    Task<DocumentParsingConfigDto?> GetActiveConfigAsync(string documentType, CancellationToken ct = default);
}
