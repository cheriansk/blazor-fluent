using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Shared.Common;
using BlazorFluent.Shared.Contracts.Engine.Analytics;
using BlazorFluent.Shared.Contracts.Engine.Calendar;
using BlazorFluent.Shared.Contracts.Engine.Client;
using BlazorFluent.Shared.Contracts.Engine.Transforms;
using BlazorFluent.Shared.Contracts.Engine.Workflows;
using BlazorFluent.Shared.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Infrastructure.EngineGateway;

/// <summary>
/// Resilient, AES-256-GCM AEAD encrypted HTTP gateway implementation connecting Client WebApp to Company Azure ProcessAPI.
/// </summary>
public class RemoteEngineGateway : IRemoteEngineGateway, IEngineClient
{
    private readonly HttpClient _httpClient;
    private readonly ITenantContext _tenantContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RemoteEngineGateway> _logger;

    public RemoteEngineGateway(
        HttpClient httpClient,
        ITenantContext tenantContext,
        IConfiguration configuration,
        ILogger<RemoteEngineGateway> logger)
    {
        _httpClient = httpClient;
        _tenantContext = tenantContext;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Result<TResp>> CallApiAsync<TReq, TResp>(string route, TReq request, CancellationToken ct = default)
    {
        try
        {
            var tenantId = _tenantContext.RequiredTenantId;
            var tenantIdStr = tenantId.ToString();

            var licenseKey = _configuration[$"Tenants:{tenantIdStr}:LicenseKey"];
            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                _logger.LogError("Zero-Trust Security Violation: No dedicated license key configured for tenant '{TenantId}'. Shared fallback is strictly prohibited.", tenantIdStr);
                throw new InvalidOperationException($"Zero-Trust Security Violation: No dedicated license key configured for tenant '{tenantIdStr}'.");
            }

            var encryptionKey = PayloadEncryptionUtility.DeriveTenantKey(licenseKey, tenantIdStr);

            // 1. Encrypt outbound request payload with AES-256-GCM AEAD
            var envelope = PayloadEncryptionUtility.EncryptPayload(request, encryptionKey);

            // 2. Transmit encrypted envelope
            var response = await _httpClient.PostAsJsonAsync(route, envelope, JsonDefaults.StandardOptions, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("ProcessAPI returned HTTP {StatusCode} on route {Route}. Response: {Response}",
                    response.StatusCode, route, errorBody);

                return Result<TResp>.Fail($"Cloud API returned HTTP {(int)response.StatusCode}: {errorBody}");
            }

            // 3. Receive encrypted envelope response
            var responseEnvelope = await response.Content.ReadFromJsonAsync<EncryptedEnvelopeDto>(JsonDefaults.StandardOptions, ct);
            if (responseEnvelope == null)
            {
                return Result<TResp>.Fail("Cloud API returned an empty or invalid envelope response.");
            }

            // 4. Decrypt and verify AEAD authentication tag (anti-tamper)
            var decryptedResponse = PayloadEncryptionUtility.DecryptPayload<TResp>(responseEnvelope, encryptionKey);
            if (decryptedResponse == null)
            {
                return Result<TResp>.Fail("Failed to deserialize decrypted Cloud API payload.");
            }

            return Result<TResp>.Ok(decryptedResponse);
        }
        catch (CryptographicException cryptoEx)
        {
            _logger.LogCritical(cryptoEx, "Payload tampering detected or key mismatch while calling route {Route}", route);
            return Result<TResp>.Fail("Security error: Cryptographic signature verification failed on API response.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error communicating with Cloud ProcessAPI on route {Route}", route);
            return Result<TResp>.Fail($"ProcessAPI connection error: {ex.Message}");
        }
    }

    // High-level IEngineClient implementations
    public Task<Result<FileConvertRespDto>> ConvertFileAsync(FileConvertReqDto request, CancellationToken ct = default) =>
        CallApiAsync<FileConvertReqDto, FileConvertRespDto>(EngineRoutes.TransformsConvert, request, ct);

    public Task<Result<ProcessBatchRespDto>> ProcessStagedBatchAsync(ProcessBatchReqDto request, CancellationToken ct = default) =>
        CallApiAsync<ProcessBatchReqDto, ProcessBatchRespDto>(EngineRoutes.TransformsProcessBatch, request, ct);

    public Task<Result<DecryptProprietaryRespDto>> DecryptProprietaryBatchAsync(DecryptProprietaryReqDto request, CancellationToken ct = default) =>
        CallApiAsync<DecryptProprietaryReqDto, DecryptProprietaryRespDto>(EngineRoutes.TransformsDecryptProprietary, request, ct);

    public Task<Result<ProgramRollupRespDto>> RollupProgramAsync(ProgramRollupReqDto request, CancellationToken ct = default) =>
        CallApiAsync<ProgramRollupReqDto, ProgramRollupRespDto>(EngineRoutes.AnalyticsRollup, request, ct);

    public Task<Result<DependencyDagRespDto>> AnalyzeDagAsync(DependencyDagReqDto request, CancellationToken ct = default) =>
        CallApiAsync<DependencyDagReqDto, DependencyDagRespDto>(EngineRoutes.AnalyticsDag, request, ct);

    public Task<Result<WorkflowTransitionRespDto>> EvaluateWorkflowTransitionAsync(WorkflowTransitionReqDto request, CancellationToken ct = default) =>
        CallApiAsync<WorkflowTransitionReqDto, WorkflowTransitionRespDto>(EngineRoutes.WorkflowsTransitions, request, ct);

    public Task<Result<CalendarScheduleRespDto>> CalculateScheduleAsync(CalendarScheduleReqDto request, CancellationToken ct = default) =>
        CallApiAsync<CalendarScheduleReqDto, CalendarScheduleRespDto>(EngineRoutes.CalendarSchedule, request, ct);

    public Task<Result<TResp>> SendAsync<TReq, TResp>(string route, TReq request, CancellationToken ct = default) =>
        CallApiAsync<TReq, TResp>(route, request, ct);
}
