using BlazorFluent.Core.Contracts;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Infrastructure.Storage;

public class ProtectedLocalStorageFormService : ILocalStorageFormService
{
    private readonly ProtectedLocalStorage _storage;
    private readonly ILogger<ProtectedLocalStorageFormService> _logger;

    public ProtectedLocalStorageFormService(
        ProtectedLocalStorage storage,
        ILogger<ProtectedLocalStorageFormService> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    public async ValueTask SaveDraftAsync<T>(string formKey, T formData)
    {
        try
        {
            await _storage.SetAsync($"draft_{formKey}", formData!);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save form draft for key '{FormKey}'", formKey);
        }
    }

    public async ValueTask<T?> LoadDraftAsync<T>(string formKey)
    {
        try
        {
            var result = await _storage.GetAsync<T>($"draft_{formKey}");
            return result.Success ? result.Value : default;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load form draft for key '{FormKey}'", formKey);
            return default;
        }
    }

    public async ValueTask ClearDraftAsync(string formKey)
    {
        try
        {
            await _storage.DeleteAsync($"draft_{formKey}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear form draft for key '{FormKey}'", formKey);
        }
    }
}
