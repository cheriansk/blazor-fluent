namespace BlazorEnterprise.Shared.Contracts.Engine.Client;

/// <summary>
/// Scoped ambient context holding the caller-originated correlation ID, circuit ID, and session ID across async flows and HTTP hops.
/// </summary>
public interface ICorrelationContext
{
    /// <summary>
    /// Gets or sets the active correlation identifier for the immediate operation / user action / navigation.
    /// In Client WebApp: renewed per navigation or discrete user action (e.g. Save, Submit).
    /// In Cloud API: strictly populated from incoming X-Correlation-Id header (never self-generated).
    /// </summary>
    string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the Blazor Server circuit or SignalR connection identifier (e.g. "q1OP-DJhWVW7UYZ...").
    /// </summary>
    string? CircuitId { get; set; }

    /// <summary>
    /// Gets or sets the authenticated user session identifier (e.g. GUID).
    /// </summary>
    string? SessionId { get; set; }

    /// <summary>
    /// Generates a fresh 12-character correlation ID, assigns it, updates any ambient Activity tag, and returns it.
    /// </summary>
    string Renew();
}

/// <summary>
/// Default in-memory implementation of ICorrelationContext.
/// </summary>
public class CorrelationContext : ICorrelationContext
{
    private string _correlationId = string.Empty;

    public string CorrelationId
    {
        get => _correlationId;
        set => _correlationId = value ?? string.Empty;
    }

    public string? CircuitId { get; set; }

    public string? SessionId { get; set; }

    public string Renew()
    {
        var newId = Guid.NewGuid().ToString("N")[..12];
        _correlationId = newId;
        var activity = System.Diagnostics.Activity.Current;
        if (activity != null)
        {
            activity.SetTag("blazorenterprise.correlation_id", newId);
        }
        return newId;
    }
}
