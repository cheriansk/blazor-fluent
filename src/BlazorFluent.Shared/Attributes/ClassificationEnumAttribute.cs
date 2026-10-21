namespace BlazorFluent.Shared.Attributes;

/// <summary>
/// Marks an enum as a descriptive classification or attribute (e.g. Priority, Role, Severity).
/// Classification enums have no state machine constraints or lifecycle transition rules,
/// and categories are optional.
/// </summary>
[AttributeUsage(AttributeTargets.Enum, AllowMultiple = false, Inherited = false)]
public sealed class ClassificationEnumAttribute : Attribute
{
    /// <summary>
    /// Optional description of the classification scheme.
    /// </summary>
    public string? Description { get; }

    public ClassificationEnumAttribute(string? description = null)
    {
        Description = description;
    }
}
