namespace BlazorFluent.Jobs.Engine;

/// <summary>
/// Pre-flight outcome for BaseEngineOperation determining whether a remote API call is needed
/// or if execution can safely short-circuit locally.
/// </summary>
public record PreProcessResult<T>(
    bool IsSuccess,
    T? Data,
    bool CanShortCircuitLocally = false,
    IReadOnlyList<string>? Errors = null
)
{
    public static PreProcessResult<T> Ok(T data, bool canShortCircuitLocally = false) =>
        new(true, data, canShortCircuitLocally, null);

    public static PreProcessResult<T> ShortCircuit(T data) =>
        new(true, data, true, null);

    public static PreProcessResult<T> Fail(string error) =>
        new(false, default, false, [error]);

    public static PreProcessResult<T> Fail(IEnumerable<string> errors) =>
        new(false, default, false, errors.ToList());
}
