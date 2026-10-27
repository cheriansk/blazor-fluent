using System;
using System.Threading;
using System.Threading.Tasks;
using BlazorEnterprise.Core.Contracts;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.Logging;

namespace BlazorEnterprise.Infrastructure.Observability;

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
            "Blazor Circuit opened: CircuitId={CircuitId}, HasActiveTenant={HasActiveTenant}, TenantId={TenantId}, IsAuthenticated={IsAuthenticated}, UserId={UserId}",
            circuit.Id,
            _tenantContext.HasActiveTenant,
            _tenantContext.HasActiveTenant ? _tenantContext.TenantId : (Guid?)null,
            _currentUser.IsAuthenticated,
            _currentUser.IsAuthenticated ? _currentUser.UserId : (string?)null);

        return base.OnCircuitOpenedAsync(circuit, cancellationToken);
    }

    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Blazor Circuit connection up: CircuitId={CircuitId}, HasActiveTenant={HasActiveTenant}, TenantId={TenantId}",
            circuit.Id,
            _tenantContext.HasActiveTenant,
            _tenantContext.HasActiveTenant ? _tenantContext.TenantId : (Guid?)null);

        return base.OnConnectionUpAsync(circuit, cancellationToken);
    }

    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Blazor Circuit connection lost (network drop or tab backgrounded): CircuitId={CircuitId}, HasActiveTenant={HasActiveTenant}, TenantId={TenantId}",
            circuit.Id,
            _tenantContext.HasActiveTenant,
            _tenantContext.HasActiveTenant ? _tenantContext.TenantId : (Guid?)null);

        return base.OnConnectionDownAsync(circuit, cancellationToken);
    }

    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        var duration = _dateTimeProvider.Now - _openedAt;
        _logger.LogInformation(
            "Blazor Circuit closed: CircuitId={CircuitId}, Duration={Duration}, HasActiveTenant={HasActiveTenant}, TenantId={TenantId}, IsAuthenticated={IsAuthenticated}, UserId={UserId}",
            circuit.Id,
            duration,
            _tenantContext.HasActiveTenant,
            _tenantContext.HasActiveTenant ? _tenantContext.TenantId : (Guid?)null,
            _currentUser.IsAuthenticated,
            _currentUser.IsAuthenticated ? _currentUser.UserId : (string?)null);

        return base.OnCircuitClosedAsync(circuit, cancellationToken);
    }
}
