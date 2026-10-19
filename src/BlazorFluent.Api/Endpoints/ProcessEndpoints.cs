using System.Security.Claims;
using System.Security.Cryptography;
using BlazorFluent.RulesEngine.Analytics;
using BlazorFluent.RulesEngine.BatchProcessing;
using BlazorFluent.RulesEngine.Calendar;
using BlazorFluent.RulesEngine.Parsers;
using BlazorFluent.RulesEngine.Workflows;
using BlazorFluent.Shared.Common;
using BlazorFluent.Shared.Contracts.Engine.Analytics;
using BlazorFluent.Shared.Contracts.Engine.Calendar;
using BlazorFluent.Shared.Contracts.Engine.Client;
using BlazorFluent.Shared.Contracts.Engine.Transforms;
using BlazorFluent.Shared.Contracts.Engine.Workflows;
using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Shared.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Hybrid;
using BlazorFluent.Api.Security;

namespace BlazorFluent.Api.Endpoints;

public static class ProcessEndpoints
{
    private const int MaxPayloadLength = 10_000_000; // 10 MB maximum payload ceiling

    public static RouteGroupBuilder MapProcessEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1")
            .WithTags("ProcessAPI")
            .RequireAuthorization()
            .RequireRateLimiting("api-global");

        // 1. File Conversion Endpoint
        group.MapPost("/transforms/convert", (
            [FromBody] EncryptedEnvelopeDto envelope,
            ClaimsPrincipal user,
            IConfiguration configuration,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("BlazorFluent.Api.Endpoints.Process.Transforms");

            if (ValidateEnvelope(envelope) is { } validationError)
                return validationError;

            try
            {
                var key = ResolveEncryptionKey(user, configuration, logger);
                var req = PayloadEncryptionUtility.DecryptPayload<FileConvertReqDto>(envelope, key);

                if (req == null)
                    return Results.BadRequest(new { error = "MalformedPayload", message = "Decryption succeeded but payload structure is invalid" });

                logger.LogInformation("Processing in-memory file conversion: {FileName} ({FileType})", req.FileName, req.FileType);

                FileConvertRespDto resp;

                if (req.FileType == ImportFileType.TaskExcel)
                {
                    var result = OpenXmlTabularParser.Parse(req.FileBytes, "Tasks", ["Title", "Status"]);
                    resp = new FileConvertRespDto(
                        SessionId: req.SessionId,
                        IsSuccess: result.IsValid,
                        TotalRows: result.ValidRows.Count + result.Errors.Count,
                        ValidRows: result.ValidRows.Count,
                        ErrorRows: result.Errors.Count,
                        Errors: result.Errors,
                        HydratedJsonPayload: result.IsValid ? JsonDefaults.ToJson(result.ValidRows) : null
                    );
                }
                else
                {
                    var result = CsvTabularParser.Parse(req.FileBytes, ["Title", "Status"]);
                    resp = new FileConvertRespDto(
                        SessionId: req.SessionId,
                        IsSuccess: result.IsValid,
                        TotalRows: result.ValidRows.Count + result.Errors.Count,
                        ValidRows: result.ValidRows.Count,
                        ErrorRows: result.Errors.Count,
                        Errors: result.Errors,
                        HydratedJsonPayload: result.IsValid ? JsonDefaults.ToJson(result.ValidRows) : null
                    );
                }

                var responseEnvelope = PayloadEncryptionUtility.EncryptPayload(resp, key);
                return Results.Ok(responseEnvelope);
            }
            catch (CryptographicException ex)
            {
                logger.LogWarning(ex, "Decryption failed for user {User}", user.Identity?.Name);
                return Results.Json(new { error = "DecryptionFailed", message = "Invalid encryption key or corrupted payload" }, statusCode: StatusCodes.Status401Unauthorized);
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.LogWarning(ex, "Unauthorized tenant claim access for user {User}", user.Identity?.Name);
                return Results.Json(new { error = "UnauthorizedTenant", message = ex.Message }, statusCode: StatusCodes.Status401Unauthorized);
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning(ex, "Tenant license violation for user {User}", user.Identity?.Name);
                return Results.Json(new { error = "LicenseViolation", message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .WithName("ConvertFile")
        .WithSummary("Convert Excel or CSV file into validated tabular entity model")
        .RequireRateLimiting("api-expensive");

        // 2. Stream Chunk Ingestion Endpoint
        group.MapPost("/transforms/stream-chunk", (
            [FromBody] EncryptedEnvelopeDto envelope,
            ClaimsPrincipal user,
            IConfiguration configuration,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("BlazorFluent.Api.Endpoints.Process.Transforms");

            if (ValidateEnvelope(envelope) is { } validationError)
                return validationError;

            try
            {
                var key = ResolveEncryptionKey(user, configuration, logger);
                var req = PayloadEncryptionUtility.DecryptPayload<FileChunkStreamReqDto>(envelope, key);

                if (req == null)
                    return Results.BadRequest(new { error = "MalformedPayload", message = "Decryption succeeded but chunk payload is invalid" });

                logger.LogInformation("Streaming chunk {Index} for session {SessionId}", req.ChunkIndex, req.SessionId);

                var resp = new FileChunkStreamRespDto(
                    SessionId: req.SessionId,
                    ChunkIndex: req.ChunkIndex,
                    Acknowledged: true,
                    ProcessedRowsCount: req.ChunkData.Length / 128,
                    Message: req.IsLastChunk ? "Final chunk acknowledged." : "Chunk acknowledged."
                );

                return Results.Ok(PayloadEncryptionUtility.EncryptPayload(resp, key));
            }
            catch (CryptographicException ex)
            {
                logger.LogWarning(ex, "Decryption failed for user {User}", user.Identity?.Name);
                return Results.Json(new { error = "DecryptionFailed", message = "Invalid encryption key or corrupted payload" }, statusCode: StatusCodes.Status401Unauthorized);
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.LogWarning(ex, "Unauthorized tenant claim access for user {User}", user.Identity?.Name);
                return Results.Json(new { error = "UnauthorizedTenant", message = ex.Message }, statusCode: StatusCodes.Status401Unauthorized);
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning(ex, "Tenant license violation for user {User}", user.Identity?.Name);
                return Results.Json(new { error = "LicenseViolation", message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .WithName("StreamChunk")
        .WithSummary("Acknowledge streamed ingestion chunk");

        // 3. Analytics Rollup Endpoint
        group.MapPost("/analytics/rollups", (
            [FromBody] EncryptedEnvelopeDto envelope,
            ClaimsPrincipal user,
            IConfiguration configuration,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("BlazorFluent.Api.Endpoints.Process.Analytics");

            if (ValidateEnvelope(envelope) is { } validationError)
                return validationError;

            try
            {
                var key = ResolveEncryptionKey(user, configuration, logger);
                var req = PayloadEncryptionUtility.DecryptPayload<ProgramRollupReqDto>(envelope, key);

                if (req == null)
                    return Results.BadRequest(new { error = "MalformedPayload", message = "Decryption succeeded but rollup payload is invalid" });

                logger.LogInformation("Computing strategic rollup for program {ProgramId}", req.ProgramId);
                var resp = ProgramRollupEngine.ComputeRollup(req);

                return Results.Ok(PayloadEncryptionUtility.EncryptPayload(resp, key));
            }
            catch (CryptographicException ex)
            {
                logger.LogWarning(ex, "Decryption failed for user {User}", user.Identity?.Name);
                return Results.Json(new { error = "DecryptionFailed", message = "Invalid encryption key or corrupted payload" }, statusCode: StatusCodes.Status401Unauthorized);
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.LogWarning(ex, "Unauthorized tenant claim access for user {User}", user.Identity?.Name);
                return Results.Json(new { error = "UnauthorizedTenant", message = ex.Message }, statusCode: StatusCodes.Status401Unauthorized);
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning(ex, "Tenant license violation for user {User}", user.Identity?.Name);
                return Results.Json(new { error = "LicenseViolation", message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .WithName("RollupProgram")
        .WithSummary("Compute multi-tier program strategic rollups")
        .RequireRateLimiting("api-expensive");

        // 4. DAG Cycle Analysis Endpoint
        group.MapPost("/analytics/dag-cycles", (
            [FromBody] EncryptedEnvelopeDto envelope,
            ClaimsPrincipal user,
            IConfiguration configuration,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("BlazorFluent.Api.Endpoints.Process.Analytics");

            if (ValidateEnvelope(envelope) is { } validationError)
                return validationError;

            try
            {
                var key = ResolveEncryptionKey(user, configuration, logger);
                var req = PayloadEncryptionUtility.DecryptPayload<DependencyDagReqDto>(envelope, key);

                if (req == null)
                    return Results.BadRequest(new { error = "MalformedPayload", message = "Decryption succeeded but DAG payload is invalid" });

                logger.LogInformation("Analyzing dependency DAG for project {ProjectId}", req.ProjectId);
                var resp = DependencyDagEngine.Analyze(req);

                return Results.Ok(PayloadEncryptionUtility.EncryptPayload(resp, key));
            }
            catch (CryptographicException ex)
            {
                logger.LogWarning(ex, "Decryption failed for user {User}", user.Identity?.Name);
                return Results.Json(new { error = "DecryptionFailed", message = "Invalid encryption key or corrupted payload" }, statusCode: StatusCodes.Status401Unauthorized);
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.LogWarning(ex, "Unauthorized tenant claim access for user {User}", user.Identity?.Name);
                return Results.Json(new { error = "UnauthorizedTenant", message = ex.Message }, statusCode: StatusCodes.Status401Unauthorized);
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning(ex, "Tenant license violation for user {User}", user.Identity?.Name);
                return Results.Json(new { error = "LicenseViolation", message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .WithName("AnalyzeDag")
        .WithSummary("Perform topological sort, critical path calculation, and circular dependency detection")
        .RequireRateLimiting("api-expensive");

        // 5. Workflow State Transition Evaluation Endpoint
        group.MapPost("/workflows/transitions", (
            [FromBody] EncryptedEnvelopeDto envelope,
            ClaimsPrincipal user,
            IConfiguration configuration,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("BlazorFluent.Api.Endpoints.Process.Workflows");

            if (ValidateEnvelope(envelope) is { } validationError)
                return validationError;

            try
            {
                var key = ResolveEncryptionKey(user, configuration, logger);
                var req = PayloadEncryptionUtility.DecryptPayload<WorkflowTransitionReqDto>(envelope, key);

                if (req == null)
                    return Results.BadRequest(new { error = "MalformedPayload", message = "Decryption succeeded but transition payload is invalid" });

                logger.LogInformation("Evaluating workflow transition for {EntityId}: {From} -> {To}",
                    req.EntityId, req.CurrentState, req.ProposedState);

                var resp = WorkflowTransitionEngine.Evaluate(req);
                return Results.Ok(PayloadEncryptionUtility.EncryptPayload(resp, key));
            }
            catch (CryptographicException ex)
            {
                logger.LogWarning(ex, "Decryption failed for user {User}", user.Identity?.Name);
                return Results.Json(new { error = "DecryptionFailed", message = "Invalid encryption key or corrupted payload" }, statusCode: StatusCodes.Status401Unauthorized);
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.LogWarning(ex, "Unauthorized tenant claim access for user {User}", user.Identity?.Name);
                return Results.Json(new { error = "UnauthorizedTenant", message = ex.Message }, statusCode: StatusCodes.Status401Unauthorized);
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning(ex, "Tenant license violation for user {User}", user.Identity?.Name);
                return Results.Json(new { error = "LicenseViolation", message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .WithName("EvaluateWorkflowTransition")
        .WithSummary("Evaluate state transition validity against state machine rules")
        .RequireClearance(ProjectRole.Lead);

        // 6. Enterprise Calendar Schedule Endpoint
        group.MapPost("/calendar/schedule", (
            [FromBody] EncryptedEnvelopeDto envelope,
            ClaimsPrincipal user,
            IConfiguration configuration,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("BlazorFluent.Api.Endpoints.Process.Calendar");

            if (ValidateEnvelope(envelope) is { } validationError)
                return validationError;

            try
            {
                var key = ResolveEncryptionKey(user, configuration, logger);
                var req = PayloadEncryptionUtility.DecryptPayload<CalendarScheduleReqDto>(envelope, key);

                if (req == null)
                    return Results.BadRequest(new { error = "MalformedPayload", message = "Decryption succeeded but calendar payload is invalid" });

                logger.LogInformation("Calculating schedule duration: {Days} working days from {Start}",
                    req.DurationDays, req.StartDateUtc);

                var resp = CalendarWorkingDayEngine.Calculate(req);
                return Results.Ok(PayloadEncryptionUtility.EncryptPayload(resp, key));
            }
            catch (CryptographicException ex)
            {
                logger.LogWarning(ex, "Decryption failed for user {User}", user.Identity?.Name);
                return Results.Json(new { error = "DecryptionFailed", message = "Invalid encryption key or corrupted payload" }, statusCode: StatusCodes.Status401Unauthorized);
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.LogWarning(ex, "Unauthorized tenant claim access for user {User}", user.Identity?.Name);
                return Results.Json(new { error = "UnauthorizedTenant", message = ex.Message }, statusCode: StatusCodes.Status401Unauthorized);
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning(ex, "Tenant license violation for user {User}", user.Identity?.Name);
                return Results.Json(new { error = "LicenseViolation", message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .WithName("CalculateCalendarSchedule")
        .WithSummary("Calculate working days, holiday exclusions, and forward/backward target schedules");

        // 7. Stateless Canonical Batch Processing Endpoint (All-or-Nothing + Proprietary At-Rest Vault Encryption)
        group.MapPost("/transforms/process-batch", async (
            [FromBody] EncryptedEnvelopeDto envelope,
            ClaimsPrincipal user,
            IConfiguration configuration,
            HybridCache hybridCache,
            CloudProcessorDispatcher dispatcher,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("BlazorFluent.Api.Endpoints.Process.Batch");

            if (ValidateEnvelope(envelope) is { } validationError)
                return validationError;

            try
            {
                var key = ResolveEncryptionKey(user, configuration, logger);
                var req = PayloadEncryptionUtility.DecryptPayload<ProcessBatchReqDto>(envelope, key);

                if (req == null)
                    return Results.BadRequest(new { error = "MalformedPayload", message = "Decryption succeeded but batch request structure is invalid" });

                CanonicalStagedChunkPayload? chunkPayload = null;
                if (req.CompressedChunkBytes is { Length: > 0 })
                {
                    var decompressedJson = PayloadEncryptionUtility.DecompressGzip(req.CompressedChunkBytes);
                    chunkPayload = JsonDefaults.FromJson<CanonicalStagedChunkPayload>(decompressedJson);
                }
                else if (req.Rows != null && req.HeaderMap != null)
                {
                    chunkPayload = new CanonicalStagedChunkPayload(
                        ImportId: req.ImportId,
                        ImportFileId: req.ImportFileId,
                        FileType: req.FileType,
                        SheetName: "Default",
                        ChunkIndex: req.ChunkIndex,
                        TotalChunks: req.TotalChunks,
                        RowCount: req.Rows.Count,
                        IsLastChunk: req.IsLastChunk,
                        HeaderMap: req.HeaderMap,
                        Rows: req.Rows);
                }

                if (chunkPayload == null)
                    return Results.BadRequest(new { error = "EmptyChunkPayload", message = "Neither Rows/HeaderMap nor CompressedChunkBytes produced a valid staged chunk." });

                logger.LogInformation(
                    "Processing staged batch chunk {ChunkIndex}/{TotalChunks} for ImportFile {ImportFileId} ({FileType}, {RowCount} rows)",
                    req.ChunkIndex, req.TotalChunks, req.ImportFileId, req.FileType, chunkPayload.Rows.Count);

                // Cache transient session metadata in HybridCache (L1 RAM + optional L2 Redis) with tenant namespace
                var activeTenant = user.Claims.FirstOrDefault(c => c.Type == "tenant_id" || c.Type == "tid")?.Value ?? "default";
                var cacheKey = $"t:{activeTenant.ToLowerInvariant()}:batch:import:{req.ImportFileId}:chunk:{req.ChunkIndex}";
                await hybridCache.SetAsync(
                    cacheKey,
                    new { req.ImportId, req.ImportFileId, req.FileType, req.ChunkIndex, ProcessedAtUtc = DateTime.UtcNow },
                    cancellationToken: ct);

                var resp = dispatcher.Dispatch(
                    req.ImportId,
                    req.ImportFileId,
                    req.ChunkIndex,
                    req.FileType,
                    chunkPayload,
                    null);

                // Check Proprietary At-Rest Byte Encryption Flag
                var encryptAtRest = req.ForceEncryptAtRest ?? configuration.GetValue<bool>("ProprietaryStorage:EncryptProcessedDataAtRest");
                if (resp.IsSuccess && encryptAtRest && resp.ProcessedRows.Count > 0)
                {
                    var vaultKey = ResolveVaultAtRestKey(user, configuration);
                    var encryptedRows = new List<ProcessedDomainRowDto>(resp.ProcessedRows.Count);

                    foreach (var row in resp.ProcessedRows)
                    {
                        var encryptedBytes = PayloadEncryptionUtility.EncryptToPackedBytes(row.DomainProperties, vaultKey);
                        var redactedProps = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                        foreach (var kvp in row.DomainProperties)
                        {
                            if (string.Equals(kvp.Key, "Title", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(kvp.Key, "Description", StringComparison.OrdinalIgnoreCase))
                            {
                                redactedProps[kvp.Key] = "[ENCRYPTED]";
                            }
                            else
                            {
                                redactedProps[kvp.Key] = kvp.Value;
                            }
                        }

                        encryptedRows.Add(row with
                        {
                            DomainProperties = redactedProps,
                            IsEncryptedAtRest = true,
                            EncryptedPayloadBytes = encryptedBytes
                        });
                    }

                    resp = resp with { ProcessedRows = encryptedRows, IsEncryptedAtRest = true };
                }

                return Results.Ok(PayloadEncryptionUtility.EncryptPayload(resp, key));
            }
            catch (CryptographicException ex)
            {
                logger.LogWarning(ex, "Decryption failed for user {User}", user.Identity?.Name);
                return Results.Json(new { error = "DecryptionFailed", message = "Invalid encryption key or corrupted payload" }, statusCode: StatusCodes.Status401Unauthorized);
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.LogWarning(ex, "Unauthorized tenant claim access for user {User}", user.Identity?.Name);
                return Results.Json(new { error = "UnauthorizedTenant", message = ex.Message }, statusCode: StatusCodes.Status401Unauthorized);
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning(ex, "Tenant license violation for user {User}", user.Identity?.Name);
                return Results.Json(new { error = "LicenseViolation", message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .WithName("ProcessStagedBatch")
        .WithSummary("Process a staged canonical JSON batch chunk with all-or-nothing atomicity and optional proprietary at-rest encryption")
        .RequireRateLimiting("api-expensive");

        // 8. On-Demand Proprietary Payload Decryption Endpoint (For authorized UI screen display)
        group.MapPost("/transforms/decrypt-proprietary", (
            [FromBody] EncryptedEnvelopeDto envelope,
            ClaimsPrincipal user,
            IConfiguration configuration,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("BlazorFluent.Api.Endpoints.Process.DecryptProprietary");

            if (ValidateEnvelope(envelope) is { } validationError)
                return validationError;

            try
            {
                var key = ResolveEncryptionKey(user, configuration, logger);
                var req = PayloadEncryptionUtility.DecryptPayload<DecryptProprietaryReqDto>(envelope, key);

                if (req == null)
                    return Results.BadRequest(new { error = "MalformedPayload", message = "Decryption succeeded but proprietary decryption request is invalid" });

                var vaultKey = ResolveVaultAtRestKey(user, configuration);
                var decryptedItems = new List<DecryptedProprietaryItemDto>(req.Items.Count);

                foreach (var item in req.Items)
                {
                    var props = PayloadEncryptionUtility.DecryptFromPackedBytes<Dictionary<string, string?>>(
                        item.EncryptedPayloadBytes,
                        vaultKey) ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

                    decryptedItems.Add(new DecryptedProprietaryItemDto(
                        RecordId: item.RecordId,
                        Properties: props));
                }

                var resp = new DecryptProprietaryRespDto(IsSuccess: true, Items: decryptedItems, ErrorMessage: null);
                return Results.Ok(PayloadEncryptionUtility.EncryptPayload(resp, key));
            }
            catch (CryptographicException ex)
            {
                logger.LogWarning(ex, "Proprietary decryption failed for user {User}", user.Identity?.Name);
                return Results.Json(new { error = "DecryptionFailed", message = "Invalid encryption key or corrupted proprietary payload" }, statusCode: StatusCodes.Status401Unauthorized);
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.LogWarning(ex, "Unauthorized tenant claim access for user {User}", user.Identity?.Name);
                return Results.Json(new { error = "UnauthorizedTenant", message = ex.Message }, statusCode: StatusCodes.Status401Unauthorized);
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning(ex, "Tenant license or vault key violation for user {User}", user.Identity?.Name);
                return Results.Json(new { error = "LicenseViolation", message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .WithName("DecryptProprietaryBatch")
        .WithSummary("Decrypt proprietary at-rest byte[] payloads in-memory for authorized UI display");

        return group;
    }

    private static IResult? ValidateEnvelope(EncryptedEnvelopeDto? envelope)
    {
        if (envelope == null ||
            string.IsNullOrWhiteSpace(envelope.CiphertextBase64) ||
            envelope.CiphertextBase64.Length > MaxPayloadLength)
        {
            return Results.BadRequest(new { error = "InvalidPayload", message = "Envelope is null or exceeds 10 MB maximum size" });
        }

        return null;
    }

    private static byte[] ResolveEncryptionKey(ClaimsPrincipal user, IConfiguration configuration, ILogger logger)
    {
        // Get tenant from authenticated JWT claim (not headers!)
        var tenantIdClaim = user.Claims.FirstOrDefault(c => c.Type == "tenant_id" || c.Type == "tid")?.Value;
        if (string.IsNullOrWhiteSpace(tenantIdClaim))
        {
            throw new UnauthorizedAccessException("Missing tenant identifier ('tenant_id' or 'tid') claim in JWT token");
        }

        // Retrieve tenant-specific license key strictly — fallback to shared key is strictly prohibited
        var licenseKey = configuration[$"Tenants:{tenantIdClaim}:LicenseKey"];
        if (string.IsNullOrWhiteSpace(licenseKey))
        {
            logger.LogError("Zero-Trust Security Violation: No tenant-specific license key configured for tenant '{TenantId}'. Shared fallback is prohibited.", tenantIdClaim);
            throw new InvalidOperationException($"Zero-Trust Security Violation: No tenant-specific license key configured for tenant '{tenantIdClaim}'. Fallback to shared license key is strictly prohibited.");
        }

        logger.LogInformation("Processing API request for tenant {TenantId} by user {UserId}",
            tenantIdClaim, user.Identity?.Name);

        return PayloadEncryptionUtility.DeriveTenantKey(licenseKey, tenantIdClaim);
    }

    private static byte[] ResolveVaultAtRestKey(ClaimsPrincipal user, IConfiguration configuration)
    {
        var tenantIdClaim = user.Claims.FirstOrDefault(c => c.Type == "tenant_id" || c.Type == "tid")?.Value;
        if (string.IsNullOrWhiteSpace(tenantIdClaim))
        {
            throw new UnauthorizedAccessException("Missing tenant identifier ('tenant_id' or 'tid') claim in JWT token");
        }

        var vaultMasterKey = configuration["ProprietaryStorage:VaultMasterKey"];
        if (string.IsNullOrWhiteSpace(vaultMasterKey))
        {
            throw new InvalidOperationException("Zero-Trust Security Violation: 'ProprietaryStorage:VaultMasterKey' must be configured when proprietary at-rest encryption is active.");
        }

        return PayloadEncryptionUtility.DeriveVaultAtRestKey(vaultMasterKey, tenantIdClaim);
    }
}
