namespace BlazorEnterprise.Shared.Attributes;

/// <summary>
/// Explicitly permits a hardcoded default property value in an entity, DTO, model, or record,
/// informing the Roslyn analyzer (TG027) that the default is intentional and governed by business rules.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class AllowHardcodedDefaultAttribute : Attribute
{
    /// <summary>
    /// Explicit justification for allowing the hardcoded default value.
    /// </summary>
    public string Reason { get; }

    public AllowHardcodedDefaultAttribute(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A valid justification reason must be provided when allowing hardcoded defaults.", nameof(reason));
        }

        Reason = reason;
    }
}
