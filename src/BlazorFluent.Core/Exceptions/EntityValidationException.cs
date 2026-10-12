namespace BlazorFluent.Core.Exceptions;

/// <summary>
/// Domain exception thrown when one or more database entities fail validation during the SaveChanges pipeline.
/// Aggregates all validation failures across all entities in the commit batch into a structured dictionary.
/// </summary>
public class EntityValidationException : Exception
{
    /// <summary>
    /// Structured collection of validation failures.
    /// Key: EntityName.PropertyName or EntityName.
    /// Value: Array of validation failure messages.
    /// </summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public EntityValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base(FormatMessage(errors))
    {
        Errors = errors;
    }

    public EntityValidationException(string entityName, string propertyName, string errorMessage)
        : this(new Dictionary<string, string[]>
        {
            [$"{entityName}.{propertyName}"] = [errorMessage]
        })
    {
    }

    private static string FormatMessage(IReadOnlyDictionary<string, string[]> errors)
    {
        var totalFailures = errors.Sum(e => e.Value.Length);
        return $"Validation failed for {errors.Count} target(s) with {totalFailures} total error(s): " +
               string.Join("; ", errors.Select(e => $"{e.Key}: [{string.Join(", ", e.Value)}]"));
    }
}
