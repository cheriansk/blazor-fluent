using BlazorFluent.Shared.Common;

namespace BlazorFluent.Infrastructure.EngineGateway;

/// <summary>
/// Gateway interface for sending encrypted, authenticated requests to Company Azure Cloud ProcessAPI.
/// </summary>
public interface IRemoteEngineGateway
{
    /// <summary>
    /// Encrypts and transmits an engine request payload over HTTPS with mandatory X-Correlation-Id and X-Tenant-License-Key.
    /// Decrypts and verifies the returned AES-256-GCM envelope response.
    /// </summary>
    Task<Result<TResp>> CallApiAsync<TReq, TResp>(string route, TReq request, CancellationToken ct = default);
}
