using System.Runtime.CompilerServices;
using BlazorFluent.Core.Common;
using BlazorFluent.Core.Domain.Events;

namespace BlazorFluent.Core.Contracts;

/// <summary>
/// Service contract for recording and querying the lifecycle of application and background events:
/// captures publication (sender class/method, author, JSON payload) and individual consumer receipts
/// (handler, execution timing, duration, failure diagnostics).
/// </summary>
public interface IEventTrackerService
{
    /// <summary>
    /// Records the dispatch of an event with automatic caller source capture.
    /// Returns the primary key (<see cref="Guid"/>) of the created publication tracker record.
    /// </summary>
    Task<Guid> TrackPublishAsync<TEvent>(
        TEvent @event,
        Guid? eventId = null,
        string? correlationId = null,
        string? triggerSource = null,
        string? sourceClass = null,
        [CallerMemberName] string sourceMethod = "",
        [CallerFilePath] string sourceFilePath = "",
        string? senderOrigin = null,
        string? senderUserId = null,
        string? senderUserEmail = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that an individual consumer has picked up an event for execution.
    /// Returns the primary key (<see cref="Guid"/>) of the consumer tracker record.
    /// </summary>
    Task<Guid> TrackConsumptionStartAsync(
        Guid eventId,
        string consumerClass,
        string consumerMethod,
        int attemptCount = 1,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records the completion of a consumer's execution with wall-clock duration and failure diagnostics.
    /// Updates the parent event's aggregate status.
    /// </summary>
    Task TrackConsumptionCompleteAsync(
        Guid consumptionTrackerId,
        bool isSuccess,
        double durationMs,
        string? errorMessage = null,
        string? exceptionDetails = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a paginated list of published events according to specified search criteria.
    /// </summary>
    Task<PagedResult<EventPublishTrackerEntity>> GetEventsAsync(
        EventTrackerFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all individual consumer receipts associated with a published event.
    /// </summary>
    Task<IReadOnlyList<EventConsumptionTrackerEntity>> GetConsumptionsAsync(
        Guid publishTrackerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single event tracker record including its consumer receipts.
    /// </summary>
    Task<EventPublishTrackerEntity?> GetEventByIdAsync(
        Guid eventTrackerId,
        CancellationToken cancellationToken = default);
}
