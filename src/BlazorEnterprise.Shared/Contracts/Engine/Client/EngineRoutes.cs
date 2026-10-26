namespace BlazorEnterprise.Shared.Contracts.Engine.Client;

/// <summary>
/// ProcessAPI route endpoints hosted in Company Azure Cloud.
/// </summary>
public static class EngineRoutes
{
    public const string BasePrefix = "/api/v1";

    public const string TransformsConvert = $"{BasePrefix}/transforms/convert";
    public const string TransformsStreamChunk = $"{BasePrefix}/transforms/stream-chunk";
    public const string TransformsProcessBatch = $"{BasePrefix}/transforms/process-batch";
    public const string TransformsDecryptProprietary = $"{BasePrefix}/transforms/decrypt-proprietary";
    public const string AnalyticsRollup = $"{BasePrefix}/analytics/rollups";
    public const string AnalyticsDag = $"{BasePrefix}/analytics/dag-cycles";
    public const string WorkflowsTransitions = $"{BasePrefix}/workflows/transitions";
    public const string CalendarSchedule = $"{BasePrefix}/calendar/schedule";
}
