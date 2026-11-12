using BlazorEnterprise.Shared.Attributes;

namespace BlazorEnterprise.Core.Dtos.Response;

/// <summary>
/// Immutable DTO record passed as payload to IDialogService.ShowDrawerAsync.
/// </summary>
public record EntityHistoryDataRespDto
{
    public string EntityName { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
    public string? PropertyName { get; init; }
    [AllowHardcodedDefault("Default title for entity change history drawer")]
    public string Title { get; init; } = "Entity Change History";
}
