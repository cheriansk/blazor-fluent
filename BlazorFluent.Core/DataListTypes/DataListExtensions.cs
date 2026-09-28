using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace BlazorFluent.Core.DataListTypes;

/// <summary>
/// Cached reflection extension methods for DataListTypes enums.
/// Dynamically resolves companion Categories and Filters definitions for high-performance O(1) attribute lookups.
/// </summary>
public static class DataListExtensions
{
    private record EnumMemberMetadata(
        string Code,
        string DisplayName,
        string? Description,
        CategoryInfo? Category,
        IReadOnlyList<FilterInfo> Filters);

    private record EnumTypeDefinitions(
        IReadOnlyDictionary<string, CategoryInfo> Categories,
        IReadOnlyDictionary<string, FilterInfo> Filters);

    private static readonly ConcurrentDictionary<Type, EnumTypeDefinitions> DefinitionsCache = new();
    private static readonly ConcurrentDictionary<(Type EnumType, object Value), EnumMemberMetadata> MemberCache = new();

    private static EnumTypeDefinitions ResolveTypeDefinitions(Type enumType)
    {
        return DefinitionsCache.GetOrAdd(enumType, static type =>
        {
            var categories = new Dictionary<string, CategoryInfo>(StringComparer.OrdinalIgnoreCase);
            var filters = new Dictionary<string, FilterInfo>(StringComparer.OrdinalIgnoreCase);

            // Convention: Look for {EnumName}Definitions, {EnumName}Meta, or {EnumName}Categories
            var possibleDefTypeNames = new[]
            {
                $"{type.FullName}Definitions",
                $"{type.Namespace}.{type.Name}Definitions",
                $"{type.FullName}Meta",
                $"{type.Namespace}.{type.Name}Meta"
            };

            Type? defType = null;
            foreach (var name in possibleDefTypeNames)
            {
                defType = type.Assembly.GetType(name, throwOnError: false);
                if (defType != null) break;
            }

            // Fallback: search exported types in assembly for companion class matching convention
            if (defType == null)
            {
                var targetName = $"{type.Name}Definitions";
                defType = type.Assembly.GetTypes()
                    .FirstOrDefault(t => string.Equals(t.Name, targetName, StringComparison.OrdinalIgnoreCase));
            }

            if (defType != null)
            {
                // Scan nested Categories class
                var catNestedType = defType.GetNestedType("Categories", BindingFlags.Public | BindingFlags.NonPublic)
                    ?? defType.Assembly.GetType($"{defType.FullName}+Categories");

                if (catNestedType != null)
                {
                    foreach (var field in catNestedType.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
                    {
                        if (field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string))
                        {
                            var code = field.GetRawConstantValue()?.ToString();
                            if (!string.IsNullOrWhiteSpace(code))
                            {
                                var display = field.GetCustomAttribute<DisplayAttribute>();
                                var displayName = !string.IsNullOrWhiteSpace(display?.Name) ? display.Name : code;
                                categories[code] = new CategoryInfo(code, displayName, display?.Description);
                            }
                        }
                    }
                }

                // Scan nested Filters class
                var filterNestedType = defType.GetNestedType("Filters", BindingFlags.Public | BindingFlags.NonPublic)
                    ?? defType.Assembly.GetType($"{defType.FullName}+Filters");

                if (filterNestedType != null)
                {
                    foreach (var field in filterNestedType.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
                    {
                        if (field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string))
                        {
                            var code = field.GetRawConstantValue()?.ToString();
                            if (!string.IsNullOrWhiteSpace(code))
                            {
                                var display = field.GetCustomAttribute<DisplayAttribute>();
                                var displayName = !string.IsNullOrWhiteSpace(display?.Name) ? display.Name : code;
                                filters[code] = new FilterInfo(code, displayName, display?.Description);
                            }
                        }
                    }
                }
            }

            return new EnumTypeDefinitions(categories, filters);
        });
    }

    private static EnumMemberMetadata ResolveMemberMetadata(Enum value)
    {
        var key = (value.GetType(), (object)value);
        return MemberCache.GetOrAdd(key, static k =>
        {
            var (type, val) = k;
            var name = val.ToString()!;
            var field = type.GetField(name);

            if (field == null)
            {
                return new EnumMemberMetadata(name, name, null, null, []);
            }

            var displayAttr = field.GetCustomAttribute<DisplayAttribute>();
            var displayName = !string.IsNullOrWhiteSpace(displayAttr?.Name)
                ? displayAttr.Name
                : name;
            var description = displayAttr?.Description;

            var typeDefs = ResolveTypeDefinitions(type);

            // Resolve Category
            CategoryInfo? categoryInfo = null;
            var catAttr = field.GetCustomAttribute<DataListCategoryAttribute>();
            if (catAttr != null)
            {
                if (typeDefs.Categories.TryGetValue(catAttr.Code, out var foundCat))
                {
                    categoryInfo = foundCat;
                }
                else
                {
                    categoryInfo = new CategoryInfo(catAttr.Code, catAttr.Code, null);
                }
            }

            // Resolve Filters
            var resolvedFilters = new List<FilterInfo>();
            var filterAttr = field.GetCustomAttribute<DataListFilterCriteriasAttribute>();
            if (filterAttr?.Criterias != null)
            {
                foreach (var filterCode in filterAttr.Criterias)
                {
                    if (typeDefs.Filters.TryGetValue(filterCode, out var foundFilter))
                    {
                        resolvedFilters.Add(foundFilter);
                    }
                    else
                    {
                        resolvedFilters.Add(new FilterInfo(filterCode, filterCode, null));
                    }
                }
            }

            return new EnumMemberMetadata(
                Code: name,
                DisplayName: displayName,
                Description: description,
                Category: categoryInfo,
                Filters: resolvedFilters);
        });
    }

    /// <summary>
    /// Gets the human-readable display name defined in [Display(Name = "...")].
    /// Falls back to the enum member code if no DisplayAttribute is declared.
    /// Never use this value for code or database comparisons.
    /// </summary>
    public static string GetDisplayName(this Enum value) =>
        ResolveMemberMetadata(value).DisplayName;

    /// <summary>
    /// Gets the optional description defined in [Display(Description = "...")].
    /// Returns null if not specified.
    /// </summary>
    public static string? GetDescription(this Enum value) =>
        ResolveMemberMetadata(value).Description;

    /// <summary>
    /// Gets the category metadata declared via [DataListCategory(Code)].
    /// Resolves Code, DisplayName, and Description from the companion Categories definition class.
    /// Returns null if the enum member does not belong to any category.
    /// </summary>
    public static CategoryInfo? GetCategory(this Enum value) =>
        ResolveMemberMetadata(value).Category;

    /// <summary>
    /// Checks whether an enum member belongs to a specific category code (case-insensitive).
    /// </summary>
    public static bool HasCategory(this Enum value, string categoryCode)
    {
        if (string.IsNullOrWhiteSpace(categoryCode)) return false;
        var category = GetCategory(value);
        return string.Equals(category?.Code, categoryCode, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the filter criteria metadata tags declared via [DataListFilterCriterias(Codes...)].
    /// Resolves Code, DisplayName, and Description from the companion Filters definition class.
    /// </summary>
    public static IReadOnlyList<FilterInfo> GetFilters(this Enum value) =>
        ResolveMemberMetadata(value).Filters;

    /// <summary>
    /// Gets the list of raw filter criteria codes required for this enum value.
    /// </summary>
    public static IReadOnlyList<string> GetFilterCriterias(this Enum value) =>
        GetFilters(value).Select(f => f.Code).ToList();

    /// <summary>
    /// Checks if the enum member matches an active filter criteria code.
    /// If the enum has NO filter criteria attribute, it is considered unrestricted and returns true.
    /// </summary>
    public static bool MatchesFilter(this Enum value, string? activeCriteria)
    {
        var filters = GetFilters(value);
        if (filters.Count == 0) return true;
        if (string.IsNullOrWhiteSpace(activeCriteria)) return false;

        return filters.Any(f => string.Equals(f.Code, activeCriteria, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Checks if the enum member matches any of the active filter criteria codes.
    /// If the enum has NO filter criteria attribute, it returns true.
    /// </summary>
    public static bool MatchesAnyFilter(this Enum value, IEnumerable<string>? activeCriterias)
    {
        var filters = GetFilters(value);
        if (filters.Count == 0) return true;
        if (activeCriterias == null) return false;

        var set = new HashSet<string>(activeCriterias, StringComparer.OrdinalIgnoreCase);
        return filters.Any(f => set.Contains(f.Code));
    }

    /// <summary>
    /// Returns filtered enum values based on optional category code and/or filter criteria.
    /// </summary>
    public static IEnumerable<TEnum> GetFilteredValues<TEnum>(
        string? categoryCode = null,
        string? filterCriteria = null) where TEnum : struct, Enum
    {
        var values = Enum.GetValues<TEnum>();
        foreach (var val in values)
        {
            if (!string.IsNullOrWhiteSpace(categoryCode) && !val.HasCategory(categoryCode))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(filterCriteria) && !val.MatchesFilter(filterCriteria))
            {
                continue;
            }

            yield return val;
        }
    }

    /// <summary>
    /// Projects enum values into DataListItem DTOs tailored for direct binding to FluentSelect or UI grids.
    /// Includes full Category and Filter metadata resolved from companion definition classes.
    /// </summary>
    public static IReadOnlyList<DataListItem<TEnum>> ToDataListItems<TEnum>(
        string? categoryCode = null,
        string? filterCriteria = null) where TEnum : struct, Enum
    {
        var items = new List<DataListItem<TEnum>>();
        foreach (var val in GetFilteredValues<TEnum>(categoryCode, filterCriteria))
        {
            var meta = ResolveMemberMetadata(val);
            items.Add(new DataListItem<TEnum>(
                Value: val,
                Code: meta.Code,
                DisplayName: meta.DisplayName,
                Description: meta.Description,
                Category: meta.Category,
                Filters: meta.Filters));
        }

        return items;
    }
}
