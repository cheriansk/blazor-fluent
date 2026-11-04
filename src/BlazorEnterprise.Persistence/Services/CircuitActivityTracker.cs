using System;
using System.Threading;
using BlazorEnterprise.Core.Contracts;

namespace BlazorEnterprise.Persistence.Services;

/// <summary>
/// Scoped thread-safe tracker maintaining current activity counts, visual busy state,
/// and ambient query cancellation tokens across the circuit lifetime.
/// </summary>
public sealed class CircuitActivityTracker : ICircuitActivityTracker, IDisposable
{
    private int _activeReadCount;
    private int _activeWriteCount;
    private CancellationTokenSource _cts = new();

    public int ActiveReadCount => Volatile.Read(ref _activeReadCount);
    public int ActiveWriteCount => Volatile.Read(ref _activeWriteCount);

    public bool IsBusy => ActiveReadCount > 0 || ActiveWriteCount > 0;
    public bool IsMutating => ActiveWriteCount > 0;

    public CancellationToken CurrentNavigationToken => _cts.Token;

    public event Action? OnChange;

    public IDisposable BeginOperation(bool isWrite = false)
    {
        if (isWrite)
        {
            Interlocked.Increment(ref _activeWriteCount);
        }
        else
        {
            Interlocked.Increment(ref _activeReadCount);
        }

        NotifyStateChanged();

        return new ActivityScope(this, isWrite);
    }

    public void ResetNavigationToken()
    {
        try
        {
            if (!_cts.IsCancellationRequested)
            {
                _cts.Cancel();
            }
        }
        catch (ObjectDisposedException)
        {
            // Ignore if disposed concurrently
        }
        finally
        {
            _cts.Dispose();
            _cts = new CancellationTokenSource();
        }
    }

    private void EndOperation(bool isWrite)
    {
        if (isWrite)
        {
            Interlocked.Decrement(ref _activeWriteCount);
        }
        else
        {
            Interlocked.Decrement(ref _activeReadCount);
        }

        NotifyStateChanged();
    }

    private void NotifyStateChanged()
    {
        OnChange?.Invoke();
    }

    public void Dispose()
    {
        try
        {
            if (!_cts.IsCancellationRequested)
            {
                _cts.Cancel();
            }
            _cts.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed
        }
    }

    private sealed class ActivityScope : IDisposable
    {
        private CircuitActivityTracker? _tracker;
        private readonly bool _isWrite;

        public ActivityScope(CircuitActivityTracker tracker, bool isWrite)
        {
            _tracker = tracker;
            _isWrite = isWrite;
        }

        public void Dispose()
        {
            var tracker = Interlocked.Exchange(ref _tracker, null);
            tracker?.EndOperation(_isWrite);
        }
    }
}
