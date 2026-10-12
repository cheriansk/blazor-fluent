namespace BlazorFluent.Core.Contracts;

public interface ILocalStorageFormService
{
    ValueTask SaveDraftAsync<T>(string formKey, T formData);
    ValueTask<T?> LoadDraftAsync<T>(string formKey);
    ValueTask ClearDraftAsync(string formKey);
}
