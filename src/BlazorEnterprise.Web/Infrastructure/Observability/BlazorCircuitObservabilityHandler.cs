using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.Logging;

namespace BlazorEnterprise.Web.Infrastructure.Observability;

/// <summary>
/// Enterprise circuit lifecycle handler tracking SignalR circuit creation, connection state,
/// disconnect events, and circuit lifetime for Blazor Server observability.
/// </summary>
public class BlazorCircuitObservabilityHandler : CircuitHandler
{
    private readonly ILogger<BlazorCircuitObservabilityHandler> _logger;

    public BlazorCircuitObservabilityHandler(ILogger<BlazorCircuitObservabilityHandler> logger)
    {
        _logger = logger;
    }

    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Blazor circuit opened. CircuitId: {CircuitId}", circuit.Id);
        return base.OnCircuitOpenedAsync(circuit, cancellationToken);
    }

    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Blazor circuit closed. CircuitId: {CircuitId}", circuit.Id);
        return base.OnCircuitClosedAsync(circuit, cancellationToken);
    }

    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Blazor circuit connection restored. CircuitId: {CircuitId}", circuit.Id);
        return base.OnConnectionUpAsync(circuit, cancellationToken);
    }

    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _logger.LogWarning("Blazor circuit connection dropped. CircuitId: {CircuitId}", circuit.Id);
        return base.OnConnectionDownAsync(circuit, cancellationToken);
    }
}
