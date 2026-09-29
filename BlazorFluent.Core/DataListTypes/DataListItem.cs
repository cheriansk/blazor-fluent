namespace BlazorFluent.Core.DataListTypes;

/// <summary>
/// Metadata for an enum category grouping.
/// </summary>
public record CategoryInfo(
    string Code,
    string DisplayName,
    string? Description = null);

/// <summary>
/// Metadata for an enum visibility filter criteria.
/// </summary>
public record FilterInfo(
    string Code,
    string DisplayName,
    string? Description = null);

/// <summary>
/// Immutable projection model representing an enum value for UI data binding (e.g., FluentSelect, grids, dropdowns).
/// </summary>
/// <typeparam name="TEnum">The underlying enum type.</typeparam>
public record DataListItem<TEnum>(
    TEnum Value,
    string Code,
    string DisplayName,
    string? Description = null,
    CategoryInfo? Category = null,
    IReadOnlyList<FilterInfo>? Filters = null
) where TEnum : struct, Enum;
