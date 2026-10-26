namespace BlazorEnterprise.Infrastructure.Security;

/// <summary>
/// Service contract for generating ephemeral (5-minute), cryptographically signed S2S JWT tokens
/// for outbound machine-to-machine calls from WebApp or Standalone Worker to Cloud ProcessAPI.
/// </summary>
public interface ITenantTokenService
{
    /// <summary>
    /// Mints a short-lived, tamper-proof S2S token signed exclusively with the tenant's license key.
    /// Exposes zero PII, zero emails, and zero database primary keys.
    /// </summary>
    /// <param name="tenantId">The active tenant identifier (tid).</param>
    /// <param name="actorType">Actor role (User, BatchWorker, or SystemDaemon).</param>
    /// <returns>Base64Url-encoded signed JWT string.</returns>
    string GenerateS2SToken(string tenantId, string actorType = "BatchWorker");

    /// <summary>
    /// Mints a short-lived, tamper-proof S2S token signed exclusively with the tenant's license key.
    /// Exposes zero PII, zero emails, and zero database primary keys.
    /// </summary>
    /// <param name="tenantId">The active tenant Guid identifier (tid).</param>
    /// <param name="actorType">Actor role (User, BatchWorker, or SystemDaemon).</param>
    /// <returns>Base64Url-encoded signed JWT string.</returns>
    string GenerateS2SToken(Guid tenantId, string actorType = "BatchWorker");
}
