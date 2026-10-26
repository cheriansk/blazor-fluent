namespace BlazorEnterprise.Shared.Attributes;

/// <summary>
/// Marks an enum as a stateful lifecycle enum representing a progression or workflow.
/// Enums decorated with this attribute MUST declare companion transition matrices
/// (e.g. AllowedTransitions dictionary) scoped by category.
/// </summary>
[AttributeUsage(AttributeTargets.Enum, AllowMultiple = false, Inherited = false)]
public sealed class LifecycleEnumAttribute : Attribute
{
    /// <summary>
    /// Optional description of the lifecycle progression.
    /// </summary>
    public string? Description { get; }

    public LifecycleEnumAttribute(string? description = null)
    {
        Description = description;
    }
}
