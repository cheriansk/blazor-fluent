using System.Security.Claims;
using System.Security.Cryptography;
using BlazorFluent.RulesEngine.Analytics;
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
        })
        .WithName("EvaluateWorkflowTransition")
        .WithSummary("Evaluate state transition validity against state machine rules");

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
        })
        .WithName("CalculateCalendarSchedule")
        .WithSummary("Calculate working days, holiday exclusions, and forward/backward target schedules");

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
        var tenantIdClaim = user.Claims.FirstOrDefault(c => c.Type == "tenant_id")?.Value;
        if (string.IsNullOrWhiteSpace(tenantIdClaim))
        {
            throw new UnauthorizedAccessException("Missing tenant_id claim in JWT token");
        }

        // Retrieve tenant-specific license key from secure config
        var licenseKey = configuration[$"Tenants:{tenantIdClaim}:LicenseKey"]
            ?? configuration["Engine:LicenseKey"];
        if (string.IsNullOrWhiteSpace(licenseKey))
        {
            throw new InvalidOperationException($"No license key configured for tenant {tenantIdClaim}");
        }

        logger.LogInformation("Processing API request for tenant {TenantId} by user {UserId}",
            tenantIdClaim, user.Identity?.Name);

        return PayloadEncryptionUtility.DeriveTenantKey(licenseKey, tenantIdClaim);
    }
}
