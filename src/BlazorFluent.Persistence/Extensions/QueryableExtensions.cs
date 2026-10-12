using System.Linq.Expressions;

namespace BlazorFluent.Persistence.Extensions;

public static class QueryableExtensions
{
    /// <summary>
    /// EF Core 10 LINQ extension for Left Outer Join operations between outer and inner entities.
    /// Simplifies optional navigation queries without nested GroupJoin/DefaultIfEmpty boilerplate.
    /// </summary>
    public static IQueryable<TResult> LeftJoin<TOuter, TInner, TKey, TResult>(
        this IQueryable<TOuter> outer,
        IQueryable<TInner> inner,
        Expression<Func<TOuter, TKey>> outerKeySelector,
        Expression<Func<TInner, TKey>> innerKeySelector,
        Expression<Func<TOuter, TInner?, TResult>> resultSelector)
    {
        return outer
            .GroupJoin(
                inner,
                outerKeySelector,
                innerKeySelector,
                (outerItem, innerGroup) => new { outerItem, innerGroup })
            .SelectMany(
                x => x.innerGroup.DefaultIfEmpty(),
                (x, innerItem) => resultSelector.Compile()(x.outerItem, innerItem))
            .AsQueryable();
    }
}
