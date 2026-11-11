namespace BlazorEnterprise.Core.Events;

public record UnitOfWorkSucceededDomainEvent(
    Guid ScopeId,
    string OperationName,
    Guid TenantId,
    string? UserId,
    string? UserEmail,
    IReadOnlyList<string> AffectedEntities,
    int EntityDomainEventCount) : DomainEventBase;

public record UnitOfWorkFailedDomainEvent(
    Guid ScopeId,
    string OperationName,
    Guid TenantId,
    string? UserId,
    string? UserEmail,
    IReadOnlyList<string> AttemptedEntities,
    IReadOnlyList<string> Errors,
    string? ExceptionType,
    string? DiagnosticDetails,
    string? IncidentReferenceId,
    bool IsConcurrencyConflict) : DomainEventBase;

public record ProgramCreatedDomainEvent(
    Guid ProgramId,
    Guid TenantId,
    string Code,
    string Name) : DomainEventBase;

public record ProgramUpdatedDomainEvent(
    Guid ProgramId,
    Guid TenantId,
    string Code,
    string Name) : DomainEventBase;

public record ProjectCreatedDomainEvent(
    Guid ProjectId,
    Guid? ProgramId,
    Guid TenantId,
    string ShortCode,
    string Name) : DomainEventBase;

public record ProjectUpdatedDomainEvent(
    Guid ProjectId,
    Guid? ProgramId,
    Guid TenantId,
    string ShortCode,
    string Name) : DomainEventBase;
