using BlazorEnterprise.Shared.Contracts.Engine.Transforms;
using BlazorEnterprise.Shared.DataListTypes;

namespace BlazorEnterprise.Web.Modules.Imports.Services;

public sealed class ImportFileTypeMetadata
{
    public ImportFileTypeEnum FileType { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string AcceptAttribute { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SampleFileName { get; set; } = string.Empty;
}

public sealed class FileTemplateDownloadResult
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public byte[] ContentBytes { get; set; } = Array.Empty<byte>();
}

public interface IFileTemplateDownloadService
{
    IReadOnlyList<ImportFileTypeMetadata> GetAvailableFileTypes();
    ImportFileTypeMetadata? GetMetadata(ImportFileTypeEnum fileType);
    Task<FileTemplateDownloadResult> GenerateTemplateAsync(ImportFileTypeEnum fileType, CancellationToken ct = default);
}
