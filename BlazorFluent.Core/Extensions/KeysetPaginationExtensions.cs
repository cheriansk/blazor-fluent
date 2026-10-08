using System.Linq.Expressions;

namespace BlazorFluent.Core.Extensions;

/// <summary>
/// Immutable response container for keyset/cursor-based pagination results.
/// Captures the current page of items along with the bookmark cursor required to fetch the subsequent page.
/// </summary>
/// <typeparam name="T">The entity or projected DTO model type.</typeparam>
/// <typeparam name="TKey">The comparable primary key or sequential timestamp cursor type (e.g., long, int, DateTime).</typeparam>
public class KeysetPagedResult<T, TKey> where TKey : struct
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int PageSize { get; init; }

    /// <summary>
    /// Cursor bookmark pointing to the last item in this page.
    /// Pass this value into the subsequent request's 'lastKey' parameter to fetch the next page in O(1) time.
    /// </summary>
    public TKey? LastKey { get; init; }

    /// <summary>
    /// Indicates whether additional records exist after this page.
    /// Evaluated in O(1) time via the 'pageSize + 1' lookahead technique without executing a separate COUNT(*) query.
    /// </summary>
    public bool HasNextPage { get; init; }

    public static KeysetPagedResult<T, TKey> Create(IReadOnlyList<T> items, int pageSize, TKey? lastKey, bool hasNextPage) =>
        new() { Items = items, PageSize = pageSize, LastKey = lastKey, HasNextPage = hasNextPage };
}

/// <summary>
/// High-performance Keyset (Cursor-based) O(1) pagination extensions for <see cref="IQueryable{T}"/>.
/// Eliminates linear O(N) database disk scanning overhead inherent to traditional OFFSET/LIMIT pagination.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why Keyset Pagination?</b><br/>
/// Traditional pagination (<c>.Skip(500000).Take(15)</c>) forces the database to read 500,015 rows from disk
/// and discard the first 500,000, causing severe performance degradation and high disk I/O on large tables.
/// In contrast, keyset pagination queries <c>WHERE Key &gt; @LastKey ORDER BY Key LIMIT @PageSize + 1</c>,
/// leveraging a direct B-Tree index seek to jump to the cursor in constant O(1) time (~1ms) regardless of table depth.
/// </para>
/// <para>
/// <b>Lookahead Optimization:</b><br/>
/// Fetches <c>pageSize + 1</c> items. If an extra row is returned, <see cref="KeysetPagedResult{T, TKey}.HasNextPage"/> is set to true
/// and the result trimmed, completely eliminating the need for an expensive <c>COUNT(*)</c> table scan.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Example 1: Fetching the initial page (first 15 items):
/// var firstPage = dbContext.AuditRecords
///     .AsNoTracking()
///     .Where(a => a.TenantId == tenantId)
///     .ToKeysetPagedResult(
///         keySelector: a => a.Id,
///         pageSize: 15,
///         lastKey: null);
///
/// // Example 2: Fetching the subsequent page ("Load More" / Infinite Scroll):
/// if (firstPage.HasNextPage)
/// {
///     var nextPage = dbContext.AuditRecords
///         .AsNoTracking()
///         .Where(a => a.TenantId == tenantId)
///         .ToKeysetPagedResult(
///             keySelector: a => a.Id,
///             pageSize: 15,
///             lastKey: firstPage.LastKey);
/// }
/// </code>
/// </example>
public static class KeysetPaginationExtensions
{
    /// <summary>
    /// Performs cursor/keyset-based O(1) pagination on an <see cref="IQueryable{T}"/> data source.
    /// Eliminates linear OFFSET scanning overhead on large multi-tenant tables.
    /// </summary>
    public static KeysetPagedResult<T, TKey> ToKeysetPagedResult<T, TKey>(
        this IQueryable<T> query,
        Expression<Func<T, TKey>> keySelector,
        int pageSize,
        TKey? lastKey = default) where TKey : struct, IComparable<TKey>
    {
        var filteredQuery = query;

        if (lastKey.HasValue)
        {
            var parameter = keySelector.Parameters[0];
            var keyProperty = keySelector.Body;
            var lastValueConstant = Expression.Constant(lastKey.Value, typeof(TKey));

            // Build item.Key > lastKey expression
            var greaterThan = Expression.GreaterThan(keyProperty, lastValueConstant);
            var lambda = Expression.Lambda<Func<T, bool>>(greaterThan, parameter);

            filteredQuery = filteredQuery.Where(lambda);
        }

        // Fetch pageSize + 1 to efficiently check if a subsequent page exists without a separate COUNT(*) query
        var rawItems = filteredQuery
            .OrderBy(keySelector)
            .Take(pageSize + 1)
            .ToList();

        var hasNextPage = rawItems.Count > pageSize;
        var items = hasNextPage ? rawItems.Take(pageSize).ToList() : rawItems;

        var keyFunc = keySelector.Compile();
        var nextLastKey = items.Count > 0 ? keyFunc(items[^1]) : (TKey?)null;

        return KeysetPagedResult<T, TKey>.Create(items, pageSize, nextLastKey, hasNextPage);
    }
}
