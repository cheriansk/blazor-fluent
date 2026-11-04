using BlazorEnterprise.Core.Contracts;

namespace BlazorEnterprise.Core.Domain.Base;

/// <summary>
/// ARCHITECTURAL WARNING: CRITICAL FRAMEWORK ROOT ENTITY
/// DO NOT MODIFY without architectural review.
/// Declares the sequential UUIDv7 primary key (Guid.CreateVersion7()) for all database entities.
/// Sequential UUIDv7 ensures optimal B-Tree index clustering and high insert throughput in PostgreSQL.
/// Implements IVersioned for PostgreSQL xmin system-column optimistic concurrency.
/// </summary>
public abstract class BaseEntity : IAuditableEntity, IVersioned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// PostgreSQL xmin system column mapped as a row-version concurrency token.
    /// Updated automatically by PostgreSQL on every physical row modification.
    /// </summary>
    public uint Version { get; set; }

    /// <summary>
    /// Creation timestamp (UTC). Stamped automatically by AuditableEntityInterceptor on insert.
    /// </summary>
    public DateTime Created { get; set; }

    /// <summary>
    /// User identifier or system actor who created the record.
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Last update timestamp (UTC). Stamped automatically by AuditableEntityInterceptor.
    /// On initial insert, matches <see cref="Created"/>.
    /// </summary>
    public DateTime Updated { get; set; }

    /// <summary>
    /// User identifier or system actor who last modified the record.
    /// On initial insert, matches <see cref="CreatedBy"/>.
    /// </summary>
    public string UpdatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether the entity has been modified since its initial creation.
    /// Evaluates to true if Updated timestamp is strictly greater than Created timestamp.
    /// </summary>
    public bool IsModifiedSinceCreation => Updated > Created;
}
