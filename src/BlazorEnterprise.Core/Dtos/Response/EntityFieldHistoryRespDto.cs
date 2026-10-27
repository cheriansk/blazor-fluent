namespace BlazorEnterprise.Core.Dtos.Response;

using BlazorEnterprise.Shared.DataListTypes;

/// <summary>
/// Immutable DTO record representing a property-level diff for entity/field history.
/// </summary>
public record EntityFieldHistoryRespDto
{
    public string AuditRecordId { get; init; } = string.Empty;
    public string EntityName { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
    public string PropertyName { get; init; } = string.Empty;
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
    public string UserId { get; init; } = string.Empty;
    public string UserEmail { get; init; } = string.Empty;
    public UserTypeEnum? UserType { get; init; }
    public EntityOperationEnum Operation { get; init; }
    public DateTime Timestamp { get; init; }
    public string? Description { get; init; }
}
