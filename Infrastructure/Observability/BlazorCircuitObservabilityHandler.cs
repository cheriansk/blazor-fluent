using BlazorFluent.Core.Contracts;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Infrastructure.Observability;

public sealed class BlazorCircuitObservabilityHandler : CircuitHandler
{
    private readonly ILogger<BlazorCircuitObservabilityHandler> _logger;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private DateTime _openedAt;

    public BlazorCircuitObservabilityHandler(
        ILogger<BlazorCircuitObservabilityHandler> logger,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
    }

    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _openedAt = _dateTimeProvider.Now;
        _logger.LogInformation(
            "Blazor Circuit opened: CircuitId={CircuitId}, TenantId={TenantId}, UserId={UserId}",
            circuit.Id, _tenantContext.TenantId ?? "uninitialized", _currentUser.UserId ?? "anonymous");

        return base.OnCircuitOpenedAsync(circuit, cancellationToken);
    }

    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Blazor Circuit connection up: CircuitId={CircuitId}, TenantId={TenantId}",
            circuit.Id, _tenantContext.TenantId ?? "uninitialized");

        return base.OnConnectionUpAsync(circuit, cancellationToken);
    }

    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Blazor Circuit connection lost (network drop or tab backgrounded): CircuitId={CircuitId}, TenantId={TenantId}",
            circuit.Id, _tenantContext.TenantId ?? "uninitialized");

        return base.OnConnectionDownAsync(circuit, cancellationToken);
    }

    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        var duration = _dateTimeProvider.Now - _openedAt;
        _logger.LogInformation(
            "Blazor Circuit closed: CircuitId={CircuitId}, Duration={Duration}, TenantId={TenantId}, UserId={UserId}",
            circuit.Id, duration, _tenantContext.TenantId ?? "uninitialized", _currentUser.UserId ?? "anonymous");

        return base.OnCircuitClosedAsync(circuit, cancellationToken);
    }
}
