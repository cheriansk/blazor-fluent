namespace BlazorFluent.Core.DataListTypes;

/// <summary>
/// Specifies filter criteria requirements for an enum member.
/// When present, the enum value is conditionally visible/available only when matching the active filter criteria context.
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class DataListFilterCriteriasAttribute : Attribute
{
    /// <summary>
    /// The criteria tags required for this enum value to be accessible.
    /// </summary>
    public IReadOnlyList<string> Criterias { get; }

    public DataListFilterCriteriasAttribute(params string[] criterias)
    {
        Criterias = criterias ?? [];
    }
}
