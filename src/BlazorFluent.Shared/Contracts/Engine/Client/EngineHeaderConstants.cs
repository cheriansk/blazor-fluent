namespace BlazorFluent.Shared.Contracts.Engine.Client;

/// <summary>
/// Mandatory HTTP headers exchanged between Client Applications and Cloud ProcessAPI.
/// </summary>
public static class EngineHeaderConstants
{
    /// <summary>
    /// Mandatory tenant license key identifying client entitlement.
    /// </summary>
    public const string LicenseKey = "X-Tenant-License-Key";

    /// <summary>
    /// Mandatory caller-supplied correlation ID. The Cloud API NEVER generates its own;
    /// incoming requests missing this header are rejected with 400 Bad Request.
    /// </summary>
    public const string CorrelationId = "X-Correlation-Id";

    /// <summary>
    /// Optional client application version header for compatibility assertions.
    /// </summary>
    public const string ClientVersion = "X-Client-Version";
}
