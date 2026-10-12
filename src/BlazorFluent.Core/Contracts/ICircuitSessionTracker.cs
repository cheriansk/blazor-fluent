namespace BlazorFluent.Core.Contracts;

/// <summary>
/// Singleton registry tracking live Blazor WebSocket circuits mapped to user session identifiers.
/// Enables real-time online/idle presence detection without polling the database.
/// </summary>
public interface ICircuitSessionTracker
{
    /// <summary>
    /// Maps an active SignalR circuit to an authenticated user session ID.
    /// </summary>
    void RegisterCircuit(string circuitId, Guid sessionId);

    /// <summary>
    /// Removes a disconnected SignalR circuit.
    /// </summary>
    void UnregisterCircuit(string circuitId);

    /// <summary>
    /// Checks whether any active SignalR WebSocket circuit is currently connected for this session ID.
    /// </summary>
    bool IsSessionConnected(Guid sessionId);

    /// <summary>
    /// Returns all session IDs that have at least one active WebSocket circuit connected.
    /// </summary>
    IReadOnlyCollection<Guid> GetConnectedSessions();
}
