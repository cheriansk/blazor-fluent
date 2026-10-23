namespace BlazorFluent.Shared.Common;

/// <summary>
/// Clamped pagination math, offset calculations, and keyset cursor normalization.
/// Standardizes pagination safety across all endpoints and services.
/// </summary>
public static class PaginationUtility
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    /// <summary>
    /// Normalizes requested page number and page size for traditional offset pagination.
    /// </summary>
    public static (int PageNumber, int PageSize, int Skip) Normalize(int pageNumber, int pageSize)
    {
        var normalizedPageNumber = Math.Max(1, pageNumber);
        var normalizedPageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);
        var skip = (normalizedPageNumber - 1) * normalizedPageSize;
        return (normalizedPageNumber, normalizedPageSize, skip);
    }

    /// <summary>
    /// Calculates the total page count for a given total record count and page size.
    /// </summary>
    public static int CalculateTotalPages(int totalCount, int pageSize)
    {
        var validPageSize = pageSize <= 0 ? DefaultPageSize : pageSize;
        return (int)Math.Ceiling(totalCount / (double)validPageSize);
    }

    /// <summary>
    /// Normalizes requested page size for keyset/cursor-based pagination to prevent unbounded queries.
    /// </summary>
    public static int NormalizeKeysetPageSize(int requestedPageSize, int defaultSize = DefaultPageSize, int maxSize = MaxPageSize)
    {
        if (requestedPageSize <= 0) return defaultSize;
        return Math.Min(requestedPageSize, maxSize);
    }
}
