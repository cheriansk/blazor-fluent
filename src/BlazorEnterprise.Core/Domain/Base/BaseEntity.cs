using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Core.Events;

namespace BlazorEnterprise.Core.Domain.Base;

/// <summary>
/// Root abstract base entity for all persistent domain entities.
/// Declares the sequential UUIDv7 primary key, concurrency token, Level 1 audit properties,
/// and Milan Jovanović's Domain Events collection.
/// </summary>
public abstract class BaseEntity : IConcurrency
{
    private readonly List<IDomainEvent> _domainEvents = [];

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
    /// </summary>
    public bool IsModifiedSinceCreation => Updated > Created;

    /// <summary>
    /// Collection of domain events raised by this entity during business operations.
    /// Harvested and dispatched automatically by UnitOfWork on commit.
    /// </summary>
    [NotMapped]
    [JsonIgnore]
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents() => _domainEvents.Clear();
}
