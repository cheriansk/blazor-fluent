using Microsoft.AspNetCore.Mvc;
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

namespace BlazorFluent.Api.Controllers;

[ApiController]
[Route("api/v1")]
public class ProcessApiController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ProcessApiController> _logger;

    public ProcessApiController(
        IConfiguration configuration,
        ILogger<ProcessApiController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    private byte[] ResolveEncryptionKey()
    {
        var licenseKey = Request.Headers[EngineHeaderConstants.LicenseKey].FirstOrDefault()
            ?? _configuration["Engine:LicenseKey"]
            ?? "BlazorFluent.Default.License";

        var tenantId = Request.Headers["X-Tenant-Id"].FirstOrDefault()
            ?? _configuration["Tenant:TenantId"]
            ?? "GlobalTenant";

        return PayloadEncryptionUtility.DeriveTenantKey(licenseKey, tenantId);
    }

    [HttpPost("transforms/convert")]
    public ActionResult<EncryptedEnvelopeDto> ConvertFile([FromBody] EncryptedEnvelopeDto envelope)
    {
        var key = ResolveEncryptionKey();
        var req = PayloadEncryptionUtility.DecryptPayload<FileConvertReqDto>(envelope, key);
        if (req == null) return BadRequest("Malformed or unparseable encrypted payload.");

        _logger.LogInformation("Processing in-memory file conversion: {FileName} ({FileType})", req.FileName, req.FileType);

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
        return Ok(responseEnvelope);
    }

    [HttpPost("transforms/stream-chunk")]
    public ActionResult<EncryptedEnvelopeDto> StreamChunk([FromBody] EncryptedEnvelopeDto envelope)
    {
        var key = ResolveEncryptionKey();
        var req = PayloadEncryptionUtility.DecryptPayload<FileChunkStreamReqDto>(envelope, key);
        if (req == null) return BadRequest("Malformed payload.");

        _logger.LogInformation("Streaming chunk {Index} for session {SessionId}", req.ChunkIndex, req.SessionId);

        var resp = new FileChunkStreamRespDto(
            SessionId: req.SessionId,
            ChunkIndex: req.ChunkIndex,
            Acknowledged: true,
            ProcessedRowsCount: req.ChunkData.Length / 128, // Ingest metric estimate
            Message: req.IsLastChunk ? "Final chunk acknowledged." : "Chunk acknowledged."
        );

        return Ok(PayloadEncryptionUtility.EncryptPayload(resp, key));
    }

    [HttpPost("analytics/rollups")]
    public ActionResult<EncryptedEnvelopeDto> RollupProgram([FromBody] EncryptedEnvelopeDto envelope)
    {
        var key = ResolveEncryptionKey();
        var req = PayloadEncryptionUtility.DecryptPayload<ProgramRollupReqDto>(envelope, key);
        if (req == null) return BadRequest("Malformed payload.");

        _logger.LogInformation("Computing strategic rollup for program {ProgramId}", req.ProgramId);
        var resp = ProgramRollupEngine.ComputeRollup(req);

        return Ok(PayloadEncryptionUtility.EncryptPayload(resp, key));
    }

    [HttpPost("analytics/dag-cycles")]
    public ActionResult<EncryptedEnvelopeDto> AnalyzeDag([FromBody] EncryptedEnvelopeDto envelope)
    {
        var key = ResolveEncryptionKey();
        var req = PayloadEncryptionUtility.DecryptPayload<DependencyDagReqDto>(envelope, key);
        if (req == null) return BadRequest("Malformed payload.");

        _logger.LogInformation("Analyzing dependency DAG for project {ProjectId}", req.ProjectId);
        var resp = DependencyDagEngine.Analyze(req);

        return Ok(PayloadEncryptionUtility.EncryptPayload(resp, key));
    }

    [HttpPost("workflows/transitions")]
    public ActionResult<EncryptedEnvelopeDto> EvaluateTransition([FromBody] EncryptedEnvelopeDto envelope)
    {
        var key = ResolveEncryptionKey();
        var req = PayloadEncryptionUtility.DecryptPayload<WorkflowTransitionReqDto>(envelope, key);
        if (req == null) return BadRequest("Malformed payload.");

        _logger.LogInformation("Evaluating workflow transition for {EntityId}: {From} -> {To}",
            req.EntityId, req.CurrentState, req.ProposedState);

        var resp = WorkflowTransitionEngine.Evaluate(req);
        return Ok(PayloadEncryptionUtility.EncryptPayload(resp, key));
    }

    [HttpPost("calendar/schedule")]
    public ActionResult<EncryptedEnvelopeDto> CalculateSchedule([FromBody] EncryptedEnvelopeDto envelope)
    {
        var key = ResolveEncryptionKey();
        var req = PayloadEncryptionUtility.DecryptPayload<CalendarScheduleReqDto>(envelope, key);
        if (req == null) return BadRequest("Malformed payload.");

        _logger.LogInformation("Calculating schedule duration: {Days} working days from {Start}",
            req.DurationDays, req.StartDateUtc);

        var resp = CalendarWorkingDayEngine.Calculate(req);
        return Ok(PayloadEncryptionUtility.EncryptPayload(resp, key));
    }
}
