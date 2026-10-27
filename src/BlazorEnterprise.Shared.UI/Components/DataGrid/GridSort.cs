using System.Linq.Expressions;

namespace BlazorEnterprise.Shared.UI.Components;

/// <summary>
/// Defines sorting criteria for a CskDataGrid column.
/// Supports both ascending and descending expressions with multiple levels of secondary sorting.
/// </summary>
public class GridSort<TGridItem>
{
    private readonly List<(LambdaExpression KeySelector, bool IsDescending)> _sortSteps = new();

    private GridSort()
    {
    }

    public static GridSort<TGridItem> ByAscending<TKey>(Expression<Func<TGridItem, TKey>> keySelector)
    {
        var sort = new GridSort<TGridItem>();
        sort._sortSteps.Add((keySelector, false));
        return sort;
    }

    public static GridSort<TGridItem> ByDescending<TKey>(Expression<Func<TGridItem, TKey>> keySelector)
    {
        var sort = new GridSort<TGridItem>();
        sort._sortSteps.Add((keySelector, true));
        return sort;
    }

    public GridSort<TGridItem> ThenAscending<TKey>(Expression<Func<TGridItem, TKey>> keySelector)
    {
        _sortSteps.Add((keySelector, false));
        return this;
    }

    public GridSort<TGridItem> ThenDescending<TKey>(Expression<Func<TGridItem, TKey>> keySelector)
    {
        _sortSteps.Add((keySelector, true));
        return this;
    }

    public IOrderedQueryable<TGridItem> Apply(IQueryable<TGridItem> query, bool invertDirection = false)
    {
        IOrderedQueryable<TGridItem>? orderedQuery = null;

        for (int i = 0; i < _sortSteps.Count; i++)
        {
            var (selector, isDescending) = _sortSteps[i];
            var direction = invertDirection ? !isDescending : isDescending;

            if (i == 0)
            {
                orderedQuery = direction
                    ? QueryableOrderByDescending(query, selector)
                    : QueryableOrderBy(query, selector);
            }
            else if (orderedQuery != null)
            {
                orderedQuery = direction
                    ? QueryableThenByDescending(orderedQuery, selector)
                    : QueryableThenBy(orderedQuery, selector);
            }
        }

        return orderedQuery ?? (IOrderedQueryable<TGridItem>)query;
    }

    private static IOrderedQueryable<TGridItem> QueryableOrderBy(IQueryable<TGridItem> source, LambdaExpression keySelector)
    {
        var method = typeof(Queryable).GetMethods()
            .First(m => m.Name == nameof(Queryable.OrderBy) && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(TGridItem), keySelector.ReturnType);

        return (IOrderedQueryable<TGridItem>)method.Invoke(null, new object[] { source, keySelector })!;
    }

    private static IOrderedQueryable<TGridItem> QueryableOrderByDescending(IQueryable<TGridItem> source, LambdaExpression keySelector)
    {
        var method = typeof(Queryable).GetMethods()
            .First(m => m.Name == nameof(Queryable.OrderByDescending) && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(TGridItem), keySelector.ReturnType);

        return (IOrderedQueryable<TGridItem>)method.Invoke(null, new object[] { source, keySelector })!;
    }

    private static IOrderedQueryable<TGridItem> QueryableThenBy(IOrderedQueryable<TGridItem> source, LambdaExpression keySelector)
    {
        var method = typeof(Queryable).GetMethods()
            .First(m => m.Name == nameof(Queryable.ThenBy) && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(TGridItem), keySelector.ReturnType);

        return (IOrderedQueryable<TGridItem>)method.Invoke(null, new object[] { source, keySelector })!;
    }

    private static IOrderedQueryable<TGridItem> QueryableThenByDescending(IOrderedQueryable<TGridItem> source, LambdaExpression keySelector)
    {
        var method = typeof(Queryable).GetMethods()
            .First(m => m.Name == nameof(Queryable.ThenByDescending) && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(TGridItem), keySelector.ReturnType);

        return (IOrderedQueryable<TGridItem>)method.Invoke(null, new object[] { source, keySelector })!;
    }
}
