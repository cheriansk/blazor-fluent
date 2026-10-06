using BlazorFluent.Core.Events;

namespace BlazorFluent.Jobs.Jobs.Catalog;

/// <summary>
/// Sample event: triggers a catalog synchronization batch for a specific tenant.
/// Pass <paramref name="TenantId"/> so the background listener can restore tenant context.
/// </summary>
public record CatalogSyncJobEvent(
    string TriggerSource,
    string? TenantId = null,
    int BatchSize = 100,
    string? CorrelationId = null,
    Guid? ParentExecutionId = null,
    string? SenderOrigin = null,
    string? SenderUserId = null,
    string? SenderUserEmail = null) : BaseJobEvent(TriggerSource, TenantId, CorrelationId, ParentExecutionId, SenderOrigin, SenderUserId, SenderUserEmail);

