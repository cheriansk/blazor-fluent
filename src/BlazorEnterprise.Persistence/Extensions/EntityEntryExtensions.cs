using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Domain.Base;
using Microsoft.EntityFrameworkCore;

namespace BlazorEnterprise.Persistence.Extensions;

/// <summary>
/// Helper record to wrap a raw uint version token into IConcurrency.
/// </summary>
public sealed record VersionHolder(uint Version) : IConcurrency
{
    public uint Version { get; set; } = Version;
}

/// <summary>
/// Entity Framework Core extensions for PostgreSQL xmin optimistic concurrency.
/// </summary>
public static class EntityEntryExtensions
{
    /// <summary>
    /// Explicitly applies the original xmin row-version from the edit model/DTO to the tracked entity.
    /// Guarantees that EF Core includes 'WHERE xmin = @originalVersion' in the UPDATE statement,
    /// detecting mid-edit modifications made by concurrent users or processes.
    /// </summary>
    public static void ApplyOriginalVersion<T>(this DbContext db, T entity, IConcurrency model)
        where T : BaseEntity
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(model);

        db.Entry(entity).Property(e => e.Version).OriginalValue = model.Version;
    }
}
