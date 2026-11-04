using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Shared.Contracts.Engine.Client;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace BlazorEnterprise.Infrastructure.Security;

/// <summary>
/// CircuitHandler that tracks the connection state of the user's Blazor circuit
/// and registers it with the singleton <see cref="ICircuitSessionTracker"/>.
/// </summary>
public sealed class UserSessionCircuitHandler : CircuitHandler
{
    private readonly ICircuitSessionTracker _circuitTracker;
    private readonly ICurrentUser _currentUser;
    private readonly IUserSessionService _sessionService;
    private readonly ICorrelationContext _correlationContext;
    private readonly ILogger<UserSessionCircuitHandler> _logger;

    public UserSessionCircuitHandler(
        ICircuitSessionTracker circuitTracker,
        ICurrentUser currentUser,
        IUserSessionService sessionService,
        ICorrelationContext correlationContext,
        ILogger<UserSessionCircuitHandler> logger)
    {
        _circuitTracker = circuitTracker;
        _currentUser = currentUser;
        _sessionService = sessionService;
        _correlationContext = correlationContext;
        _logger = logger;
    }

    public override async Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _correlationContext.CircuitId = circuit.Id;
        if (Guid.TryParse(_currentUser.SessionId, out var sessionId))
        {
            _correlationContext.SessionId = sessionId.ToString();
            _circuitTracker.RegisterCircuit(circuit.Id, sessionId);
            await _sessionService.UpdateHeartbeatAsync(sessionId, cancellationToken);
            using (LogContext.PushProperty("CircuitId", circuit.Id))
            using (LogContext.PushProperty("SessionId", sessionId.ToString()))
            {
                _logger.LogDebug("Circuit {CircuitId} connected for Session {SessionId}", circuit.Id, sessionId);
            }
        }

        await base.OnConnectionUpAsync(circuit, cancellationToken);
    }

    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _circuitTracker.UnregisterCircuit(circuit.Id);
        using (LogContext.PushProperty("CircuitId", circuit.Id))
        {
            _logger.LogDebug("Circuit {CircuitId} closed and unregistered from tracker.", circuit.Id);
        }
        return base.OnCircuitClosedAsync(circuit, cancellationToken);
    }

    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _circuitTracker.UnregisterCircuit(circuit.Id);
        using (LogContext.PushProperty("CircuitId", circuit.Id))
        {
            _logger.LogDebug("Circuit {CircuitId} connection down.", circuit.Id);
        }
        return base.OnConnectionDownAsync(circuit, cancellationToken);
    }
}
