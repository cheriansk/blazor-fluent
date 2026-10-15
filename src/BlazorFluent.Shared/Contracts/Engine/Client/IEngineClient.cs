using BlazorFluent.Shared.Common;
using BlazorFluent.Shared.Contracts.Engine.Analytics;
using BlazorFluent.Shared.Contracts.Engine.Calendar;
using BlazorFluent.Shared.Contracts.Engine.Transforms;
using BlazorFluent.Shared.Contracts.Engine.Workflows;

namespace BlazorFluent.Shared.Contracts.Engine.Client;

/// <summary>
/// High-level client facade for communicating with ProcessAPI and proprietary calculations.
/// </summary>
public interface IEngineClient
{
    Task<Result<FileConvertRespDto>> ConvertFileAsync(FileConvertReqDto request, CancellationToken ct = default);
    Task<Result<ProgramRollupRespDto>> RollupProgramAsync(ProgramRollupReqDto request, CancellationToken ct = default);
    Task<Result<DependencyDagRespDto>> AnalyzeDagAsync(DependencyDagReqDto request, CancellationToken ct = default);
    Task<Result<WorkflowTransitionRespDto>> EvaluateWorkflowTransitionAsync(WorkflowTransitionReqDto request, CancellationToken ct = default);
    Task<Result<CalendarScheduleRespDto>> CalculateScheduleAsync(CalendarScheduleReqDto request, CancellationToken ct = default);
    Task<Result<TResp>> SendAsync<TReq, TResp>(string route, TReq request, CancellationToken ct = default);
}
