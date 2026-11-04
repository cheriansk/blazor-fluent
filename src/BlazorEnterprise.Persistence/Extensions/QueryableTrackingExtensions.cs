using Microsoft.EntityFrameworkCore;

namespace BlazorEnterprise.Persistence.Extensions;

/// <summary>
/// Universal query tracking extension that standardizes the template's fail-closed architecture.
/// Explicitly applies <see cref="EntityFrameworkQueryableExtensions.AsTracking{TEntity}(IQueryable{TEntity})"/>
/// when <paramref name="forEdit"/> is <c>true</c>, or <see cref="EntityFrameworkQueryableExtensions.AsNoTracking{TEntity}(IQueryable{TEntity})"/>
/// when <paramref name="forEdit"/> is <c>false</c>.
/// </summary>
public static class QueryableTrackingExtensions
{
    public static IQueryable<TEntity> WithTracking<TEntity>(this IQueryable<TEntity> query, bool forEdit)
        where TEntity : class
    {
        return forEdit ? query.AsTracking() : query.AsNoTracking();
    }
}
