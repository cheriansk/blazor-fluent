using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.Logging;
using BlazorEnterprise.Shared.Contracts.Engine.Client;
using Serilog.Context;

namespace BlazorEnterprise.Web.Infrastructure.Observability;

/// <summary>
/// Enterprise circuit lifecycle handler tracking SignalR circuit creation, connection state,
/// disconnect events, and circuit lifetime for Blazor Server observability.
/// Synchronizes CircuitId with the ambient scoped ICorrelationContext and Serilog LogContext.
/// </summary>
public class BlazorCircuitObservabilityHandler : CircuitHandler
{
    private readonly ILogger<BlazorCircuitObservabilityHandler> _logger;
    private readonly ICorrelationContext _correlationContext;

    public BlazorCircuitObservabilityHandler(
        ILogger<BlazorCircuitObservabilityHandler> logger,
        ICorrelationContext correlationContext)
    {
        _logger = logger;
        _correlationContext = correlationContext;
    }

    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _correlationContext.CircuitId = circuit.Id;
        using (LogContext.PushProperty("CircuitId", circuit.Id))
        {
            _logger.LogInformation("Blazor circuit opened. CircuitId: {CircuitId}", circuit.Id);
        }
        return base.OnCircuitOpenedAsync(circuit, cancellationToken);
    }

    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        using (LogContext.PushProperty("CircuitId", circuit.Id))
        {
            _logger.LogInformation("Blazor circuit closed. CircuitId: {CircuitId}", circuit.Id);
        }
        return base.OnCircuitClosedAsync(circuit, cancellationToken);
    }

    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _correlationContext.CircuitId = circuit.Id;
        using (LogContext.PushProperty("CircuitId", circuit.Id))
        {
            _logger.LogDebug("Blazor circuit connection restored. CircuitId: {CircuitId}", circuit.Id);
        }
        return base.OnConnectionUpAsync(circuit, cancellationToken);
    }

    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        using (LogContext.PushProperty("CircuitId", circuit.Id))
        {
            _logger.LogWarning("Blazor circuit connection dropped. CircuitId: {CircuitId}", circuit.Id);
        }
        return base.OnConnectionDownAsync(circuit, cancellationToken);
    }
}
