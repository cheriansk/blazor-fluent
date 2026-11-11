using System;
using System.Collections.Generic;
using BlazorEnterprise.Core.Domain.Base;
using BlazorEnterprise.Core.Domain.Tenancy;

namespace BlazorEnterprise.Core.Events;

/// <summary>
/// Factory delegate definition for creating a domain event from a modified or added entity.
/// </summary>
public sealed record EntityEventFactory(
    Func<BaseEntity, IDomainEvent>? CreateOnInsert = null,
    Func<BaseEntity, IDomainEvent>? CreateOnUpdate = null);

/// <summary>
/// Central declarative registry mapping domain entities to their lifecycle domain events.
/// When entities are committed in a UnitOfWork transaction, TransactionContext inspects
/// this registry and automatically synthesizes any missing domain events so developers
/// never have to write repetitive boilerplate code across service methods.
/// </summary>
public static class EntityDomainEventRegistry
{
    public static readonly IReadOnlyDictionary<Type, EntityEventFactory> ConfiguredEntities =
        new Dictionary<Type, EntityEventFactory>
        {
            [typeof(ProgramEntity)] = new(
                CreateOnInsert: entity =>
                {
                    var p = (ProgramEntity)entity;
                    return new ProgramCreatedDomainEvent(p.Id, p.TenantId, p.Code, p.Name);
                },
                CreateOnUpdate: entity =>
                {
                    var p = (ProgramEntity)entity;
                    return new ProgramUpdatedDomainEvent(p.Id, p.TenantId, p.Code, p.Name);
                }
            ),
            [typeof(ProjectEntity)] = new(
                CreateOnInsert: entity =>
                {
                    var p = (ProjectEntity)entity;
                    return new ProjectCreatedDomainEvent(p.Id, p.ProgramId, p.TenantId, p.ShortCode, p.Name);
                },
                CreateOnUpdate: entity =>
                {
                    var p = (ProjectEntity)entity;
                    return new ProjectUpdatedDomainEvent(p.Id, p.ProgramId, p.TenantId, p.ShortCode, p.Name);
                }
            )
        };
}
