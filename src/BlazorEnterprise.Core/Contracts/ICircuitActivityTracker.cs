using System;
using System.Threading;

namespace BlazorEnterprise.Core.Contracts;

/// <summary>
/// Scoped circuit activity tracker coordinating database operations, visual busy states,
/// and navigation cancellation/blocking across Blazor Server circuits.
/// </summary>
public interface ICircuitActivityTracker
{
    /// <summary>
    /// Current count of active asynchronous read queries executing on this circuit.
    /// </summary>
    int ActiveReadCount { get; }

    /// <summary>
    /// Current count of active asynchronous mutation/save operations executing on this circuit.
    /// </summary>
    int ActiveWriteCount { get; }

    /// <summary>
    /// True when any query or mutation is actively executing.
    /// </summary>
    bool IsBusy { get; }

    /// <summary>
    /// True when a database save or mutation transaction is actively executing.
    /// </summary>
    bool IsMutating { get; }

    /// <summary>
    /// Ambient cancellation token tied to the current page/view. Cancelled when the user switches views.
    /// </summary>
    CancellationToken CurrentNavigationToken { get; }

    /// <summary>
    /// Broadcast event fired whenever busy, mutating, or count state changes.
    /// </summary>
    event Action? OnChange;

    /// <summary>
    /// Begins tracking an asynchronous database operation. Dispose the returned token to complete tracking.
    /// </summary>
    IDisposable BeginOperation(bool isWrite = false);

    /// <summary>
    /// Cancels any in-flight database operations tied to the current navigation token and renews the token.
    /// </summary>
    void ResetNavigationToken();
}
