namespace BlazorFluent.Shared.Common;

/// <summary>
/// Clamped pagination math and offset calculations.
/// </summary>
public static class PaginationUtility
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static (int PageNumber, int PageSize, int Skip) Normalize(int pageNumber, int pageSize)
    {
        var normalizedPageNumber = Math.Max(1, pageNumber);
        var normalizedPageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);
        var skip = (normalizedPageNumber - 1) * normalizedPageSize;
        return (normalizedPageNumber, normalizedPageSize, skip);
    }

    public static int CalculateTotalPages(int totalCount, int pageSize)
    {
        var validPageSize = pageSize <= 0 ? DefaultPageSize : pageSize;
        return (int)Math.Ceiling(totalCount / (double)validPageSize);
    }
}
