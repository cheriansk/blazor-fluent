namespace BlazorFluent.Core.DTOs;

using BlazorFluent.Core.DataListTypes;

/// <summary>
/// Immutable DTO record representing a property-level diff for entity/field history.
/// </summary>
public record EntityFieldHistoryDto
{
    public string AuditRecordId { get; init; } = string.Empty;
    public string EntityName { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
    public string PropertyName { get; init; } = string.Empty;
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
    public string UserId { get; init; } = string.Empty;
    public string UserEmail { get; init; } = string.Empty;
    public UserType? UserType { get; init; }
    public EntityOperation Operation { get; init; }
    public DateTime Timestamp { get; init; }
    public string? Description { get; init; }
}
