using BlazorEnterprise.Shared.Common;
using BlazorEnterprise.Shared.Contracts.Engine.Analytics;
using BlazorEnterprise.Shared.Contracts.Engine.Calendar;
using BlazorEnterprise.Shared.Contracts.Engine.Transforms;
using BlazorEnterprise.Shared.Contracts.Engine.Workflows;

namespace BlazorEnterprise.Shared.Contracts.Engine.Client;

/// <summary>
/// High-level client facade for communicating with ProcessAPI and proprietary calculations.
/// </summary>
public interface IEngineClient
{
    Task<Result<FileConvertRespDto>> ConvertFileAsync(FileConvertReqDto request, CancellationToken ct = default);
    Task<Result<ProcessBatchRespDto>> ProcessStagedBatchAsync(ProcessBatchReqDto request, CancellationToken ct = default);
    Task<Result<DecryptProprietaryRespDto>> DecryptProprietaryBatchAsync(DecryptProprietaryReqDto request, CancellationToken ct = default);
    Task<Result<ProgramRollupRespDto>> RollupProgramAsync(ProgramRollupReqDto request, CancellationToken ct = default);
    Task<Result<DependencyDagRespDto>> AnalyzeDagAsync(DependencyDagReqDto request, CancellationToken ct = default);
    Task<Result<WorkflowTransitionRespDto>> EvaluateWorkflowTransitionAsync(WorkflowTransitionReqDto request, CancellationToken ct = default);
    Task<Result<CalendarScheduleRespDto>> CalculateScheduleAsync(CalendarScheduleReqDto request, CancellationToken ct = default);
    Task<Result<TResp>> SendAsync<TReq, TResp>(string route, TReq request, CancellationToken ct = default);
}
