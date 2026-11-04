namespace BlazorEnterprise.Persistence.Services;

/// <summary>
/// Domain exception thrown when a concurrency conflict occurs, carrying field-level property diffs
/// for side-by-side human-in-the-loop inspection.
/// </summary>
public class ConcurrencyConflictException : Exception
{
    public IReadOnlyList<PropertyDiffItem> Diffs { get; }

    public ConcurrencyConflictException(string message, IReadOnlyList<PropertyDiffItem>? diffs = null)
        : base(message)
    {
        Diffs = diffs ?? Array.Empty<PropertyDiffItem>();
    }
}
