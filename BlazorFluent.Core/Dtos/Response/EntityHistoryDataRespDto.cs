namespace BlazorFluent.Core.Dtos.Response;

/// <summary>
/// Immutable DTO record passed as payload to IDialogService.ShowDrawerAsync.
/// </summary>
public record EntityHistoryDataRespDto
{
    public string EntityName { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
    public string? PropertyName { get; init; }
    public string Title { get; init; } = "Entity Change History";
}
