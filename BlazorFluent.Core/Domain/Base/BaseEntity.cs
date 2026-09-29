namespace BlazorFluent.Core.Domain.Base;

/// <summary>
/// ⚠️ ARCHITECTURAL WARNING: CRITICAL FRAMEWORK ROOT ENTITY
/// DO NOT MODIFY without architectural review.
/// Declares the sequential UUIDv7 primary key (Guid.CreateVersion7()) for all database entities.
/// Sequential UUIDv7 ensures optimal B-Tree index clustering and high insert throughput in PostgreSQL.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
}
