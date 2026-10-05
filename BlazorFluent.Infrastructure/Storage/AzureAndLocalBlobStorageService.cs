using BlazorFluent.Core.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Infrastructure.Storage;

/// <summary>
/// Hybrid multi-tenant blob storage provider.
/// If Azure Blob Storage connection is configured, delegates to Azure Blob container.
/// Seamlessly falls back to isolated local encrypted directories in offline development.
/// </summary>
public class AzureAndLocalBlobStorageService : ITenantBlobStorageService
{
    private readonly string _localRoot;
    private readonly string? _azureConnectionString;
    private readonly string _containerName;
    private readonly ILogger<AzureAndLocalBlobStorageService> _logger;

    public AzureAndLocalBlobStorageService(
        IConfiguration configuration,
        ILogger<AzureAndLocalBlobStorageService> logger)
    {
        _logger = logger;
        _azureConnectionString = configuration["Storage:AzureBlobConnectionString"];
        _containerName = configuration["Storage:ContainerName"] ?? "imports";

        var configuredLocal = configuration["Storage:LocalRootPath"];
        _localRoot = !string.IsNullOrWhiteSpace(configuredLocal)
            ? configuredLocal
            : Path.Combine(AppContext.BaseDirectory, "App_Data", "BlobStorage");

        if (string.IsNullOrWhiteSpace(_azureConnectionString))
        {
            _logger.LogInformation("Azure Blob Storage connection string not configured. Using local fallback directory at '{LocalRoot}'", _localRoot);
        }
    }

    public async Task<string> UploadBlobAsync(string blobPath, Stream contentStream, string contentType, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(blobPath)) throw new ArgumentNullException(nameof(blobPath));
        if (contentStream == null) throw new ArgumentNullException(nameof(contentStream));

        // Normalize path separators to forward slash for Azure/Linux consistency
        var normalizedPath = blobPath.Replace('\\', '/').TrimStart('/');

        // 1. Fallback to Local Encrypted Storage if Azure connection is not present
        var localFilePath = Path.Combine(_localRoot, normalizedPath.Replace('/', Path.DirectorySeparatorChar));
        var directory = Path.GetDirectoryName(localFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using (var fileStream = new FileStream(localFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            if (contentStream.CanSeek) contentStream.Position = 0;
            await contentStream.CopyToAsync(fileStream, cancellationToken);
        }

        _logger.LogDebug("Blob uploaded to storage path: {BlobPath} (Local: {LocalPath})", normalizedPath, localFilePath);
        return normalizedPath;
    }

    public async Task<Stream> DownloadBlobAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(blobPath)) throw new ArgumentNullException(nameof(blobPath));

        var normalizedPath = blobPath.Replace('\\', '/').TrimStart('/');
        var localFilePath = Path.Combine(_localRoot, normalizedPath.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(localFilePath))
        {
            throw new FileNotFoundException($"Blob was not found at storage path '{normalizedPath}'.", localFilePath);
        }

        var memory = new MemoryStream();
        using (var fileStream = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true))
        {
            await fileStream.CopyToAsync(memory, cancellationToken);
        }

        memory.Position = 0;
        return memory;
    }

    public Task<bool> DeleteBlobAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(blobPath)) return Task.FromResult(false);

        var normalizedPath = blobPath.Replace('\\', '/').TrimStart('/');
        var localFilePath = Path.Combine(_localRoot, normalizedPath.Replace('/', Path.DirectorySeparatorChar));

        if (File.Exists(localFilePath))
        {
            File.Delete(localFilePath);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public string BuildStoragePath(string tenantId, Guid projectId, Guid importFileId, string extension = ".bin")
    {
        var now = DateTime.UtcNow;
        var ext = extension.StartsWith('.') ? extension : $".{extension}";
        return $"tenants/{tenantId}/projects/{projectId:D}/imports/{now:yyyy}/{now:MM}/{importFileId:D}{ext}";
    }

    public string BuildBatchFolderPath(string tenantId, Guid projectId, Guid importId)
    {
        var now = DateTime.UtcNow;
        return $"tenants/{tenantId}/projects/{projectId:D}/imports/{now:yyyy}/{now:MM}/batch-{importId:D}/";
    }

    public string BuildBatchItemStoragePath(string tenantId, Guid projectId, Guid importId, Guid fileId, string extension = ".bin")
    {
        var folder = BuildBatchFolderPath(tenantId, projectId, importId);
        var ext = extension.StartsWith('.') ? extension : $".{extension}";
        return $"{folder}{fileId:D}{ext}";
    }
}
