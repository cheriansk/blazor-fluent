using BlazorEnterprise.Shared.Attributes;
using BlazorEnterprise.Shared.DataListTypes;
using BlazorEnterprise.Shared.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;

namespace BlazorEnterprise.Shared.Extensions;

/// <summary>
/// Cached reflection extension methods for DataListTypes enums.
/// Dynamically resolves companion Categories and Filters definitions for high-performance O(1) attribute lookups,
/// reverse lookups from integer or string code, and category member enumerations.
/// </summary>
public static class DataListExtensions
{
    private record EnumMemberMetadata(
        string Code,
        string DisplayName,
        string? Description,
        CategoryInfo? Category,
        IReadOnlyList<CategoryInfo> Categories,
        IReadOnlyList<FilterInfo> Filters,
        int IntValue,
        object EnumValue);

    private record EnumTypeDefinitions(
        IReadOnlyDictionary<string, CategoryInfo> Categories,
        IReadOnlyDictionary<string, FilterInfo> Filters,
        IReadOnlyDictionary<int, EnumMemberMetadata> IntToMetadata,
        IReadOnlyDictionary<string, EnumMemberMetadata> CodeToMetadata,
        IReadOnlyDictionary<string, IReadOnlyList<EnumMemberMetadata>> CategoryToMembers,
        IReadOnlyList<EnumMemberMetadata> AllMembers,
        IReadOnlyList<CategoryInfo> AllCategories,
        IReadOnlyDictionary<string, Dictionary<object, HashSet<object>>> TransitionsByCategory,
        IReadOnlyDictionary<object, HashSet<object>> DefaultTransitions);

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

            // Scan all members of the enum
            var intToMeta = new Dictionary<int, EnumMemberMetadata>();
            var codeToMeta = new Dictionary<string, EnumMemberMetadata>(StringComparer.OrdinalIgnoreCase);
            var categoryToMembers = new Dictionary<string, List<EnumMemberMetadata>>(StringComparer.OrdinalIgnoreCase);
            var allMembers = new List<EnumMemberMetadata>();

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (!field.IsLiteral) continue;

                var name = field.Name;
                var rawVal = field.GetRawConstantValue();
                var intVal = Convert.ToInt32(rawVal);
                var enumVal = field.GetValue(null)!;

                var displayAttr = field.GetCustomAttribute<DisplayAttribute>();
                var displayName = !string.IsNullOrWhiteSpace(displayAttr?.Name) ? displayAttr.Name : name;
                var description = displayAttr?.Description;

                // Resolve Categories (multi-category support)
                var catAttrs = field.GetCustomAttributes<DataListCategoryAttribute>().ToList();
                var memberCategories = new List<CategoryInfo>();
                foreach (var catAttr in catAttrs)
                {
                    if (categories.TryGetValue(catAttr.Code, out var foundCat))
                    {
                        memberCategories.Add(foundCat);
                    }
                    else
                    {
                        memberCategories.Add(new CategoryInfo(catAttr.Code, catAttr.Code, null));
                    }
                }
                var categoryInfo = memberCategories.FirstOrDefault();

                // Resolve Filters
                var resolvedFilters = new List<FilterInfo>();
                var filterAttr = field.GetCustomAttribute<DataListFilterCriteriasAttribute>();
                if (filterAttr?.Criterias != null)
                {
                    foreach (var filterCode in filterAttr.Criterias)
                    {
                        if (filters.TryGetValue(filterCode, out var foundFilter))
                        {
                            resolvedFilters.Add(foundFilter);
                        }
                        else
                        {
                            resolvedFilters.Add(new FilterInfo(filterCode, filterCode, null));
                        }
                    }
                }

                var meta = new EnumMemberMetadata(
                    Code: name,
                    DisplayName: displayName,
                    Description: description,
                    Category: categoryInfo,
                    Categories: memberCategories,
                    Filters: resolvedFilters,
                    IntValue: intVal,
                    EnumValue: enumVal);

                intToMeta[intVal] = meta;
                codeToMeta[name] = meta;
                allMembers.Add(meta);

                foreach (var cat in memberCategories)
                {
                    if (!categoryToMembers.TryGetValue(cat.Code, out var list))
                    {
                        list = new List<EnumMemberMetadata>();
                        categoryToMembers[cat.Code] = list;
                    }
                    list.Add(meta);
                }

                // Pre-populate MemberCache for quick member resolution
                MemberCache.TryAdd((type, enumVal), meta);
            }

            var readonlyCategoryToMembers = categoryToMembers.ToDictionary(
                kvp => kvp.Key,
                kvp => (IReadOnlyList<EnumMemberMetadata>)kvp.Value.AsReadOnly(),
                StringComparer.OrdinalIgnoreCase);

            var transitionsByCategory = new Dictionary<string, Dictionary<object, HashSet<object>>>(StringComparer.OrdinalIgnoreCase);
            var defaultTransitions = new Dictionary<object, HashSet<object>>();

            if (defType != null)
            {
                var transMember = defType.GetField("AllowedTransitions", BindingFlags.Public | BindingFlags.Static)
                    ?? (MemberInfo?)defType.GetProperty("AllowedTransitions", BindingFlags.Public | BindingFlags.Static);

                object? transObj = transMember switch
                {
                    FieldInfo f => f.GetValue(null),
                    PropertyInfo p => p.GetValue(null),
                    _ => null
                };

                if (transObj is System.Collections.IDictionary dict)
                {
                    foreach (System.Collections.DictionaryEntry entry in dict)
                    {
                        if (entry.Key is string categoryKey && entry.Value is System.Collections.IDictionary innerDict)
                        {
                            var catMap = new Dictionary<object, HashSet<object>>();
                            foreach (System.Collections.DictionaryEntry innerEntry in innerDict)
                            {
                                var fromState = innerEntry.Key;
                                var toSet = new HashSet<object>();
                                if (innerEntry.Value is System.Collections.IEnumerable enumerable)
                                {
                                    foreach (var toState in enumerable)
                                    {
                                        toSet.Add(toState);
                                    }
                                }
                                catMap[fromState] = toSet;
                            }
                            transitionsByCategory[categoryKey] = catMap;
                        }
                        else if (entry.Key != null)
                        {
                            var fromState = entry.Key;
                            var toSet = new HashSet<object>();
                            if (entry.Value is System.Collections.IEnumerable enumerable)
                            {
                                foreach (var toState in enumerable)
                                {
                                    toSet.Add(toState);
                                }
                            }
                            defaultTransitions[fromState] = toSet;
                        }
                    }
                }
            }

            return new EnumTypeDefinitions(
                Categories: categories,
                Filters: filters,
                IntToMetadata: intToMeta,
                CodeToMetadata: codeToMeta,
                CategoryToMembers: readonlyCategoryToMembers,
                AllMembers: allMembers,
                AllCategories: categories.Values.ToList(),
                TransitionsByCategory: transitionsByCategory,
                DefaultTransitions: defaultTransitions);
        });
    }

    private static EnumMemberMetadata ResolveMemberMetadata(Enum value)
    {
        var key = (value.GetType(), (object)value);
        if (MemberCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        // Trigger definition resolution which populates MemberCache
        _ = ResolveTypeDefinitions(value.GetType());

        if (MemberCache.TryGetValue(key, out var resolved))
        {
            return resolved;
        }

        var name = value.ToString();
        var fallback = new EnumMemberMetadata(name, name, null, null, [], [], Convert.ToInt32(value), value);
        MemberCache.TryAdd(key, fallback);
        return fallback;
    }

    #region Requirement 1: Display Name & Description Extensions

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
    /// Gets the human-readable display name defined in [Display(Name = "...")] for the integer value of an enum.
    /// Falls back to the enum member code or integer string if no display attribute is declared.
    /// </summary>
    public static string GetEnumDisplayName<TEnum>(this int value) where TEnum : struct, Enum
    {
        var defs = ResolveTypeDefinitions(typeof(TEnum));
        return defs.IntToMetadata.TryGetValue(value, out var meta) ? meta.DisplayName : value.ToString();
    }

    /// <summary>
    /// Gets the human-readable display name for a nullable integer value of an enum.
    /// Returns the provided fallback (default empty string) if null or not found.
    /// </summary>
    public static string GetEnumDisplayName<TEnum>(this int? value, string fallback = "") where TEnum : struct, Enum
    {
        if (!value.HasValue) return fallback;
        var defs = ResolveTypeDefinitions(typeof(TEnum));
        return defs.IntToMetadata.TryGetValue(value.Value, out var meta) ? meta.DisplayName : fallback;
    }

    /// <summary>
    /// Gets the optional description defined in [Display(Description = "...")] for the integer value of an enum.
    /// Returns null if not specified or undefined.
    /// </summary>
    public static string? GetEnumDescription<TEnum>(this int value) where TEnum : struct, Enum
    {
        var defs = ResolveTypeDefinitions(typeof(TEnum));
        return defs.IntToMetadata.TryGetValue(value, out var meta) ? meta.Description : null;
    }

    /// <summary>
    /// Gets the optional description for a nullable integer value of an enum.
    /// Returns null if null, unspecified, or undefined.
    /// </summary>
    public static string? GetEnumDescription<TEnum>(this int? value) where TEnum : struct, Enum
    {
        if (!value.HasValue) return null;
        var defs = ResolveTypeDefinitions(typeof(TEnum));
        return defs.IntToMetadata.TryGetValue(value.Value, out var meta) ? meta.Description : null;
    }

    /// <summary>
    /// Gets the human-readable display name defined in [Display(Name = "...")] based on the enum member code.
    /// Lookup is case-insensitive. Falls back to code or empty string.
    /// </summary>
    public static string GetEnumDisplayName<TEnum>(this string? code) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(code)) return string.Empty;
        var defs = ResolveTypeDefinitions(typeof(TEnum));
        return defs.CodeToMetadata.TryGetValue(code.Trim(), out var meta) ? meta.DisplayName : code;
    }

    /// <summary>
    /// Gets the optional description defined in [Display(Description = "...")] based on the enum member code.
    /// Lookup is case-insensitive. Returns null if not specified or undefined.
    /// </summary>
    public static string? GetEnumDescription<TEnum>(this string? code) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var defs = ResolveTypeDefinitions(typeof(TEnum));
        return defs.CodeToMetadata.TryGetValue(code.Trim(), out var meta) ? meta.Description : null;
    }

    #endregion

    #region Requirement 2: Enum Code from Integer & Value Conversions

    /// <summary>
    /// Gets the enum member code (name) for the integer value, or null if undefined.
    /// </summary>
    public static string? GetEnumCode<TEnum>(this int value) where TEnum : struct, Enum
    {
        var defs = ResolveTypeDefinitions(typeof(TEnum));
        return defs.IntToMetadata.TryGetValue(value, out var meta) ? meta.Code : null;
    }

    /// <summary>
    /// Gets the enum member code (name) for a nullable integer value, or null if null or undefined.
    /// </summary>
    public static string? GetEnumCode<TEnum>(this int? value) where TEnum : struct, Enum
    {
        if (!value.HasValue) return null;
        var defs = ResolveTypeDefinitions(typeof(TEnum));
        return defs.IntToMetadata.TryGetValue(value.Value, out var meta) ? meta.Code : null;
    }

    /// <summary>
    /// Casts an integer to the specified enum type with boundary validation.
    /// Throws ArgumentOutOfRangeException if the integer is not a defined value in TEnum.
    /// </summary>
    public static TEnum ToEnum<TEnum>(this int value) where TEnum : struct, Enum
    {
        var defs = ResolveTypeDefinitions(typeof(TEnum));
        if (defs.IntToMetadata.TryGetValue(value, out var meta))
        {
            return (TEnum)meta.EnumValue;
        }
        throw new ArgumentOutOfRangeException(nameof(value), $"Value '{value}' is not defined in enum '{typeof(TEnum).Name}'.");
    }

    /// <summary>
    /// Casts an integer to the specified enum type, returning the provided default value if the integer is undefined.
    /// </summary>
    public static TEnum ToEnum<TEnum>(this int value, TEnum defaultValue) where TEnum : struct, Enum
    {
        var defs = ResolveTypeDefinitions(typeof(TEnum));
        return defs.IntToMetadata.TryGetValue(value, out var meta) ? (TEnum)meta.EnumValue : defaultValue;
    }

    /// <summary>
    /// Converts a nullable integer to the specified enum type, returning null if the input is null or undefined.
    /// </summary>
    public static TEnum? ToEnumOrNull<TEnum>(this int? value) where TEnum : struct, Enum
    {
        if (!value.HasValue) return null;
        var defs = ResolveTypeDefinitions(typeof(TEnum));
        return defs.IntToMetadata.TryGetValue(value.Value, out var meta) ? (TEnum)meta.EnumValue : null;
    }

    /// <summary>
    /// Attempts to convert an integer to the specified enum type without throwing exceptions.
    /// </summary>
    public static bool TryToEnum<TEnum>(this int value, out TEnum result) where TEnum : struct, Enum
    {
        var defs = ResolveTypeDefinitions(typeof(TEnum));
        if (defs.IntToMetadata.TryGetValue(value, out var meta))
        {
            result = (TEnum)meta.EnumValue;
            return true;
        }
        result = default;
        return false;
    }

    /// <summary>
    /// Parses a string code into the specified enum type (case-insensitive).
    /// Throws ArgumentException if the code is invalid or undefined.
    /// </summary>
    public static TEnum ToEnum<TEnum>(this string? code) where TEnum : struct, Enum
    {
        if (!string.IsNullOrWhiteSpace(code))
        {
            var defs = ResolveTypeDefinitions(typeof(TEnum));
            if (defs.CodeToMetadata.TryGetValue(code.Trim(), out var meta))
            {
                return (TEnum)meta.EnumValue;
            }
        }
        throw new ArgumentException($"Code '{code}' is not defined in enum '{typeof(TEnum).Name}'.", nameof(code));
    }

    /// <summary>
    /// Parses a string code into the specified enum type (case-insensitive), returning defaultValue if invalid or undefined.
    /// </summary>
    public static TEnum ToEnum<TEnum>(this string? code, TEnum defaultValue) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(code)) return defaultValue;
        var defs = ResolveTypeDefinitions(typeof(TEnum));
        return defs.CodeToMetadata.TryGetValue(code.Trim(), out var meta) ? (TEnum)meta.EnumValue : defaultValue;
    }

    /// <summary>
    /// Parses a nullable string code into a nullable enum type (case-insensitive), returning null if empty or undefined.
    /// </summary>
    public static TEnum? ToEnumOrNull<TEnum>(this string? code) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var defs = ResolveTypeDefinitions(typeof(TEnum));
        return defs.CodeToMetadata.TryGetValue(code.Trim(), out var meta) ? (TEnum)meta.EnumValue : null;
    }

    /// <summary>
    /// Attempts to parse a string code into the specified enum type (case-insensitive) without throwing exceptions.
    /// </summary>
    public static bool TryToEnum<TEnum>(this string? code, out TEnum result) where TEnum : struct, Enum
    {
        if (!string.IsNullOrWhiteSpace(code))
        {
            var defs = ResolveTypeDefinitions(typeof(TEnum));
            if (defs.CodeToMetadata.TryGetValue(code.Trim(), out var meta))
            {
                result = (TEnum)meta.EnumValue;
                return true;
            }
        }
        result = default;
        return false;
    }

    /// <summary>
    /// Attempts to parse a string code into the specified enum type (case-insensitive) using cached metadata without direct Enum.TryParse.
    /// Complies strictly with TG013 architectural standard.
    /// </summary>
    public static bool TryToEnum(this string? code, Type enumType, out object? result)
    {
        if (!string.IsNullOrWhiteSpace(code) && enumType.IsEnum)
        {
            var defs = ResolveTypeDefinitions(enumType);
            if (defs.CodeToMetadata.TryGetValue(code.Trim(), out var meta))
            {
                result = meta.EnumValue;
                return true;
            }
        }
        result = null;
        return false;
    }

    /// <summary>
    /// Attempts to parse an integer value into the specified enum type using cached metadata without direct Enum.TryParse.
    /// Complies strictly with TG013 architectural standard.
    /// </summary>
    public static bool TryToEnum(this int value, Type enumType, out object? result)
    {
        if (enumType.IsEnum)
        {
            var defs = ResolveTypeDefinitions(enumType);
            if (defs.IntToMetadata.TryGetValue(value, out var meta))
            {
                result = meta.EnumValue;
                return true;
            }
        }
        result = null;
        return false;
    }

    /// <summary>
    /// Returns the cached string code representation for an enum member (avoiding repeated ToString allocations).
    /// </summary>
    public static string GetCode<TEnum>(this TEnum value) where TEnum : struct, Enum =>
        ResolveMemberMetadata(value).Code;

    /// <summary>
    /// Returns the underlying integer representation for an enum member.
    /// </summary>
    public static int ToInt<TEnum>(this TEnum value) where TEnum : struct, Enum =>
        Convert.ToInt32(value);

    #endregion

    #region Requirement 3: Category-based Lookups & Metadata

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
    /// Returns all enum member codes belonging to the specified category code (case-insensitive).
    /// </summary>
    public static IReadOnlyList<string> GetEnumCodesByCategory<TEnum>(string categoryCode) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(categoryCode)) return Array.Empty<string>();
        var defs = ResolveTypeDefinitions(typeof(TEnum));
        return defs.CategoryToMembers.TryGetValue(categoryCode.Trim(), out var members)
            ? members.Select(m => m.Code).ToArray()
            : Array.Empty<string>();
    }

    /// <summary>
    /// Returns all enum values belonging to the specified category code (case-insensitive).
    /// </summary>
    public static IReadOnlyList<TEnum> GetEnumValuesByCategory<TEnum>(string categoryCode) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(categoryCode)) return Array.Empty<TEnum>();
        var defs = ResolveTypeDefinitions(typeof(TEnum));
        return defs.CategoryToMembers.TryGetValue(categoryCode.Trim(), out var members)
            ? members.Select(m => (TEnum)m.EnumValue).ToArray()
            : Array.Empty<TEnum>();
    }

    /// <summary>
    /// Projects enum members belonging to the specified category code directly into DataListItem DTOs.
    /// </summary>
    public static IReadOnlyList<DataListItem<TEnum>> GetDataListItemsByCategory<TEnum>(string categoryCode) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(categoryCode)) return Array.Empty<DataListItem<TEnum>>();
        var defs = ResolveTypeDefinitions(typeof(TEnum));
        return defs.CategoryToMembers.TryGetValue(categoryCode.Trim(), out var members)
            ? members.Select(m => new DataListItem<TEnum>(
                Value: (TEnum)m.EnumValue,
                Code: m.Code,
                DisplayName: m.DisplayName,
                Description: m.Description,
                Category: m.Category,
                Filters: m.Filters)).ToArray()
            : Array.Empty<DataListItem<TEnum>>();
    }

    /// <summary>
    /// Gets all companion categories declared for the specified enum type.
    /// </summary>
    public static IReadOnlyList<CategoryInfo> GetCategories<TEnum>() where TEnum : struct, Enum =>
        ResolveTypeDefinitions(typeof(TEnum)).AllCategories;

    /// <summary>
    /// Gets all unique category codes declared for the specified enum type.
    /// </summary>
    public static IReadOnlyList<string> GetCategoryCodes<TEnum>() where TEnum : struct, Enum =>
        ResolveTypeDefinitions(typeof(TEnum)).Categories.Keys.ToArray();

    #endregion

    #region Filters & DataList Binding Projections

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
        var defs = ResolveTypeDefinitions(typeof(TEnum));
        IEnumerable<EnumMemberMetadata> candidates;

        if (!string.IsNullOrWhiteSpace(categoryCode))
        {
            candidates = defs.CategoryToMembers.TryGetValue(categoryCode.Trim(), out var catMembers)
                ? catMembers
                : Enumerable.Empty<EnumMemberMetadata>();
        }
        else
        {
            candidates = defs.AllMembers;
        }

        foreach (var meta in candidates)
        {
            var val = (TEnum)meta.EnumValue;
            if (!string.IsNullOrWhiteSpace(filterCriteria) && !val.MatchesFilter(filterCriteria))
            {
                continue;
            }

            yield return val;
        }
    }

    /// <summary>
    /// Projects enum values into DataListItem Dtos tailored for direct binding to FluentSelect or UI grids.
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
                Filters: meta.Filters,
                Categories: meta.Categories));
        }

        return items;
    }

    /// <summary>
    /// Retrieves all enum values projected as DataListItem Dtos as-is without filtering.
    /// </summary>
    public static IReadOnlyList<DataListItem<TEnum>> GetDataListItems<TEnum>() where TEnum : struct, Enum =>
        ToDataListItems<TEnum>(categoryCode: null, filterCriteria: null);

    /// <summary>
    /// Retrieves enum values projected as DataListItem Dtos filtered by a specific filter criteria code.
    /// If the enum does not declare filter criteria or the criteria is null/empty, returns all items.
    /// </summary>
    public static IReadOnlyList<DataListItem<TEnum>> GetFilteredDataListItems<TEnum>(string? filterCriteria) where TEnum : struct, Enum =>
        ToDataListItems<TEnum>(categoryCode: null, filterCriteria: filterCriteria);

    /// <summary>
    /// Extension method on an enum instance to retrieve all items of its enum type as DataListItem Dtos.
    /// </summary>
    public static IReadOnlyList<DataListItem<TEnum>> GetDataListItems<TEnum>(this TEnum _) where TEnum : struct, Enum =>
        GetDataListItems<TEnum>();

    /// <summary>
    /// Extension method on an enum instance to retrieve items of its enum type filtered by criteria.
    /// </summary>
    public static IReadOnlyList<DataListItem<TEnum>> GetFilteredDataListItems<TEnum>(this TEnum _, string? filterCriteria) where TEnum : struct, Enum =>
        GetFilteredDataListItems<TEnum>(filterCriteria);

    #endregion

    #region Requirement: State Transitions & Taxonomy

    /// <summary>
    /// Checks whether an enum is decorated with [LifecycleEnum].
    /// </summary>
    public static bool IsLifecycleEnum<TEnum>() where TEnum : struct, Enum =>
        typeof(TEnum).IsDefined(typeof(LifecycleEnumAttribute), inherit: false);

    /// <summary>
    /// Checks whether an enum is decorated with [ClassificationEnum].
    /// </summary>
    public static bool IsClassificationEnum<TEnum>() where TEnum : struct, Enum =>
        typeof(TEnum).IsDefined(typeof(ClassificationEnumAttribute), inherit: false);

    /// <summary>
    /// Checks whether an enum member belongs to the specified category code.
    /// </summary>
    public static bool BelongsToCategory<TEnum>(this TEnum value, string? categoryCode) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(categoryCode)) return true;
        var meta = ResolveMemberMetadata(value);
        return meta.Categories.Any(c => string.Equals(c.Code, categoryCode.Trim(), StringComparison.OrdinalIgnoreCase)) ||
               (meta.Category != null && string.Equals(meta.Category.Code, categoryCode.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets all legal next states for the current enum value within an optional category context.
    /// </summary>
    public static IReadOnlySet<TEnum> GetAllowedNextStates<TEnum>(this TEnum current, string? category = null) where TEnum : struct, Enum
    {
        var defs = ResolveTypeDefinitions(typeof(TEnum));

        if (!string.IsNullOrWhiteSpace(category) && defs.TransitionsByCategory.TryGetValue(category.Trim(), out var catDict))
        {
            if (catDict.TryGetValue(current, out var nextSet))
            {
                return new HashSet<TEnum>(nextSet.Cast<TEnum>());
            }
            return new HashSet<TEnum>();
        }

        if (defs.DefaultTransitions.TryGetValue(current, out var defSet))
        {
            return new HashSet<TEnum>(defSet.Cast<TEnum>());
        }

        return new HashSet<TEnum>();
    }

    /// <summary>
    /// Checks if a transition from current to next is permitted within an optional category context.
    /// </summary>
    public static bool CanTransitionTo<TEnum>(this TEnum current, TEnum next, string? category = null) where TEnum : struct, Enum
    {
        if (EqualityComparer<TEnum>.Default.Equals(current, next)) return true;
        var allowed = current.GetAllowedNextStates(category);
        return allowed.Contains(next);
    }

    #endregion
}
