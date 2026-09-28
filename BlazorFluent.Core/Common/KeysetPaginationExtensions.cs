using System.Linq.Expressions;

namespace BlazorFluent.Core.Common;

public class KeysetPagedResult<T, TKey> where TKey : struct
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int PageSize { get; init; }
    public TKey? LastKey { get; init; }
    public bool HasNextPage { get; init; }

    public static KeysetPagedResult<T, TKey> Create(IReadOnlyList<T> items, int pageSize, TKey? lastKey, bool hasNextPage) =>
        new() { Items = items, PageSize = pageSize, LastKey = lastKey, HasNextPage = hasNextPage };
}

public static class KeysetPaginationExtensions
{
    /// <summary>
    /// Performs cursor/keyset-based O(1) pagination on an IQueryable data source.
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
