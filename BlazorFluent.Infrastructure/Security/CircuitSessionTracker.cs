using System.Collections.Concurrent;
using BlazorFluent.Core.Contracts;

namespace BlazorFluent.Infrastructure.Security;

/// <summary>
/// Thread-safe singleton registry mapping live Blazor WebSocket circuits to active session IDs.
/// </summary>
public class CircuitSessionTracker : ICircuitSessionTracker
{
    private readonly ConcurrentDictionary<string, Guid> _circuitToSession = new();
    private readonly ConcurrentDictionary<Guid, HashSet<string>> _sessionToCircuits = new();
    private readonly object _lock = new();

    public void RegisterCircuit(string circuitId, Guid sessionId)
    {
        _circuitToSession[circuitId] = sessionId;
        lock (_lock)
        {
            if (!_sessionToCircuits.TryGetValue(sessionId, out var circuits))
            {
                circuits = new HashSet<string>();
                _sessionToCircuits[sessionId] = circuits;
            }
            circuits.Add(circuitId);
        }
    }

    public void UnregisterCircuit(string circuitId)
    {
        if (_circuitToSession.TryRemove(circuitId, out var sessionId))
        {
            lock (_lock)
            {
                if (_sessionToCircuits.TryGetValue(sessionId, out var circuits))
                {
                    circuits.Remove(circuitId);
                    if (circuits.Count == 0)
                    {
                        _sessionToCircuits.TryRemove(sessionId, out _);
                    }
                }
            }
        }
    }

    public bool IsSessionConnected(Guid sessionId)
    {
        lock (_lock)
        {
            return _sessionToCircuits.TryGetValue(sessionId, out var circuits) && circuits.Count > 0;
        }
    }

    public IReadOnlyCollection<Guid> GetConnectedSessions()
    {
        lock (_lock)
        {
            return _sessionToCircuits.Keys.ToList();
        }
    }
}
