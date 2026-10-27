namespace BlazorEnterprise.Shared.Contracts.Engine.Client;

/// <summary>
/// Scoped ambient context holding the caller-originated correlation ID across async flows and HTTP hops.
/// </summary>
public interface ICorrelationContext
{
    /// <summary>
    /// Gets or sets the active correlation identifier.
    /// In Client WebApp: generated once per user session/action or batch job trigger.
    /// In Cloud API: strictly populated from incoming X-Correlation-Id header (never self-generated).
    /// </summary>
    string CorrelationId { get; set; }
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
}
