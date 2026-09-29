namespace BlazorFluent.Core.Domain.Base;

/// <summary>
/// Temporal LINQ and in-memory extension methods for entities implementing IEffectiveDatedEntity.
/// </summary>
public static class EffectiveDatedExtensions
{
    /// <summary>
    /// Checks if the entity is active at a specified UTC point in time.
    /// Returns true if StartDate &lt;= asOfUtc AND (EndDate is null OR EndDate &gt;= asOfUtc).
    /// </summary>
    public static bool IsActiveAt(this IEffectiveDatedEntity entity, DateTime asOfUtc) =>
        entity.StartDate <= asOfUtc && (entity.EndDate == null || entity.EndDate >= asOfUtc);

    /// <summary>
    /// Checks if the entity is currently active at DateTime.UtcNow.
    /// </summary>
    public static bool IsCurrentlyActive(this IEffectiveDatedEntity entity) =>
        entity.IsActiveAt(DateTime.UtcNow);

    /// <summary>
    /// Filters an IQueryable sequence to only include records that are active as of the specified date (defaults to DateTime.UtcNow).
    /// Generates efficient SQL WHERE clause: (StartDate &lt;= target AND (EndDate IS NULL OR EndDate &gt;= target)).
    /// </summary>
    public static IQueryable<T> WhereActive<T>(this IQueryable<T> query, DateTime? asOfUtc = null)
        where T : class, IEffectiveDatedEntity
    {
        var target = asOfUtc ?? DateTime.UtcNow;
        return query.Where(e => e.StartDate <= target && (e.EndDate == null || e.EndDate >= target));
    }
}
