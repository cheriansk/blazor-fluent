using BlazorFluent.Core.Events;

namespace BlazorFluent.Jobs.Jobs.Catalog;
/// <summary>
/// SAMPLE EVENT ONLY
/// </summary>
/// <param name="TriggerSource"></param>
/// <param name="BatchSize"></param>
public record CatalogSyncJobEvent(string TriggerSource, int BatchSize = 100) : BaseJobEvent(TriggerSource);
