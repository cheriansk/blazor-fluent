using BlazorFluent.Infrastructure.EngineGateway;
using BlazorFluent.Shared.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Jobs.Engine;

/// <summary>
/// Template method pipeline base class for engine operations across local staging and cloud processing.
/// Implements 4-step pipeline: PreProcess -> ShortCircuit Check -> Mode Decision (Remote vs Local) -> PostProcess.
/// </summary>
public abstract class BaseEngineOperation<TReq, TResp>
{
    protected readonly IRemoteEngineGateway _remoteGateway;
    protected readonly IConfiguration _config;
    protected readonly ILogger _logger;
    protected readonly string _route;

    protected BaseEngineOperation(
        IRemoteEngineGateway remoteGateway,
        IConfiguration config,
        ILogger logger,
        string route)
    {
        _remoteGateway = remoteGateway;
        _config = config;
        _logger = logger;
        _route = route;
    }

    public async Task<Result<TResp>> ExecuteAsync(TReq request, CancellationToken ct = default)
    {
        try
        {
            // -------------------------------------------------------------
            // STEP 1: Pre-Processing (Validation, sanitization, hashing)
            // -------------------------------------------------------------
            var pre = await PreProcessAsync(request, ct);
            if (!pre.IsSuccess)
            {
                return Result<TResp>.Fail(pre.Errors ?? ["Pre-processing failed."]);
            }

            // -------------------------------------------------------------
            // STEP 2: Short-Circuit Check (Skip remote call if cached/empty)
            // -------------------------------------------------------------
            if (pre.CanShortCircuitLocally)
            {
                _logger.LogInformation("Engine operation short-circuited locally: {Operation}", GetType().Name);
                var localShortResult = await ExecuteLocalAsync(pre.Data!, ct);
                return await PostProcessAsync(localShortResult, ct);
            }

            // -------------------------------------------------------------
            // STEP 3: Mode Decision (Local-Only client vs Remote ProcessAPI)
            // -------------------------------------------------------------
            TResp response;
            var mode = _config.GetValue<string>("Engine:Mode") ?? "Remote";

            if (string.Equals(mode, "LocalOnly", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Executing locally (LocalOnly mode): {Operation}", GetType().Name);
                response = await ExecuteLocalAsync(pre.Data!, ct);
            }
            else
            {
                _logger.LogInformation("Delegating to Company Azure ProcessAPI with AES-256-GCM encryption on route {Route}: {Operation}", 
                    _route, GetType().Name);

                var apiResult = await _remoteGateway.CallApiAsync<TReq, TResp>(_route, pre.Data!, ct);
                if (!apiResult.IsSuccess)
                {
                    return Result<TResp>.Fail(apiResult.Errors);
                }
                response = apiResult.Data!;
            }

            // -------------------------------------------------------------
            // STEP 4: Post-Processing (PostgreSQL batch staging, entity hydration)
            // -------------------------------------------------------------
            return await PostProcessAsync(response, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Engine execution failed: {Operation}", GetType().Name);
            return Result<TResp>.Fail($"Engine execution failed: {ex.Message}");
        }
    }

    protected virtual Task<PreProcessResult<TReq>> PreProcessAsync(TReq req, CancellationToken ct) =>
        Task.FromResult(PreProcessResult<TReq>.Ok(req));

    protected abstract Task<TResp> ExecuteLocalAsync(TReq req, CancellationToken ct);

    protected virtual Task<Result<TResp>> PostProcessAsync(TResp resp, CancellationToken ct) =>
        Task.FromResult(Result<TResp>.Ok(resp));
}
