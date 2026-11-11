using BlazorEnterprise.Core.Events;
using Microsoft.Extensions.Logging;

namespace BlazorEnterprise.Persistence.Services;

public class UnitOfWorkLifecycleEventHandlers :
    IDomainEventHandler<UnitOfWorkSucceededDomainEvent>,
    IDomainEventHandler<UnitOfWorkFailedDomainEvent>,
    IDomainEventHandler<ProgramCreatedDomainEvent>,
    IDomainEventHandler<ProgramUpdatedDomainEvent>,
    IDomainEventHandler<ProjectCreatedDomainEvent>,
    IDomainEventHandler<ProjectUpdatedDomainEvent>
{
    private readonly ILogger<UnitOfWorkLifecycleEventHandlers> _logger;

    public UnitOfWorkLifecycleEventHandlers(ILogger<UnitOfWorkLifecycleEventHandlers> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(UnitOfWorkSucceededDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "DOMAIN EVENT: UnitOfWork [{Operation}] succeeded on Tenant {TenantId}. User={User}. Staged domain events: {EventCount}. Affected entities: [{Entities}]",
            domainEvent.OperationName, domainEvent.TenantId, domainEvent.UserEmail ?? domainEvent.UserId ?? "system",
            domainEvent.EntityDomainEventCount, string.Join(", ", domainEvent.AffectedEntities));
        return Task.CompletedTask;
    }

    public Task HandleAsync(UnitOfWorkFailedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "DOMAIN EVENT: UnitOfWork [{Operation}] ROLLED BACK on Tenant {TenantId}. ConcurrencyConflict={Conflict}. Attempted entities: [{Entities}]. Errors: [{Errors}]. Diagnostics: {Diag}. IncidentReference={Ref}",
            domainEvent.OperationName, domainEvent.TenantId, domainEvent.IsConcurrencyConflict,
            string.Join(", ", domainEvent.AttemptedEntities),
            string.Join("; ", domainEvent.Errors), domainEvent.DiagnosticDetails ?? "None",
            domainEvent.IncidentReferenceId ?? "N/A");
        return Task.CompletedTask;
    }

    public Task HandleAsync(ProgramCreatedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("DOMAIN EVENT: Program '{Name}' ({Code}) created with Id={ProgramId} on Tenant {TenantId}.",
            domainEvent.Name, domainEvent.Code, domainEvent.ProgramId, domainEvent.TenantId);
        return Task.CompletedTask;
    }

    public Task HandleAsync(ProgramUpdatedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("DOMAIN EVENT: Program '{Name}' ({Code}) updated with Id={ProgramId} on Tenant {TenantId}.",
            domainEvent.Name, domainEvent.Code, domainEvent.ProgramId, domainEvent.TenantId);
        return Task.CompletedTask;
    }

    public Task HandleAsync(ProjectCreatedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("DOMAIN EVENT: Project '{Name}' ({ShortCode}) created with Id={ProjectId} on Tenant {TenantId}. ProgramId={ProgramId}",
            domainEvent.Name, domainEvent.ShortCode, domainEvent.ProjectId, domainEvent.TenantId, domainEvent.ProgramId);
        return Task.CompletedTask;
    }

    public Task HandleAsync(ProjectUpdatedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("DOMAIN EVENT: Project '{Name}' ({ShortCode}) updated with Id={ProjectId} on Tenant {TenantId}. ProgramId={ProgramId}",
            domainEvent.Name, domainEvent.ShortCode, domainEvent.ProjectId, domainEvent.TenantId, domainEvent.ProgramId);
        return Task.CompletedTask;
    }
}
