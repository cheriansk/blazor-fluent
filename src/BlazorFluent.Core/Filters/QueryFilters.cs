namespace BlazorFluent.Core.Filters;

/// <summary>
/// Stable names for EF Core 10 Named Global Query Filters.
/// Allows queries to selectively disable specific filters (e.g. IgnoreQueryFilters(QueryFilters.SoftDelete))
/// while keeping tenant isolation (QueryFilters.Tenant) strictly intact.
/// </summary>
public static class QueryFilters
{
    /// <summary>
    /// Named filter enforcing tenant data isolation (or host bypass).
    /// </summary>
    public const string Tenant = "TenantFilter";

    /// <summary>
    /// Named filter suppressing soft-deleted records (!IsDeleted).
    /// </summary>
    public const string SoftDelete = "SoftDeleteFilter";
}
