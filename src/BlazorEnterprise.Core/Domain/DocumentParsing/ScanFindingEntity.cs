using BlazorEnterprise.Core.Domain.Base;

namespace BlazorEnterprise.Core.Domain.DocumentParsing;

/// <summary>
/// Persisted domain finding extracted from a Scan Findings document and processed via Stage 2b.
/// </summary>
public class ScanFindingEntity : ProjectScopedEntity, ISoftDeletableEntity, IUnversionedEntity
{
    public Guid ImportFileId { get; set; }
    public Guid? DocumentParsingResultId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public string? SourceVersion { get; set; }
    public string? TargetVersion { get; set; }
    public string? ProjectManager { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public int OrderIndex { get; set; }
    public byte[]? EncryptedPayloadBytes { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}
