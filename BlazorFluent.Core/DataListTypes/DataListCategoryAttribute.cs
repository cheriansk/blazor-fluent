namespace BlazorFluent.Core.DataListTypes;

/// <summary>
/// Categorizes an enum member by specifying its category code.
/// The category's DisplayName and optional Description are defined in the companion Categories definition class.
/// An enum entry can belong to at most one category (enforced by AllowMultiple = false).
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class DataListCategoryAttribute : Attribute
{
    /// <summary>
    /// Unique machine-readable category code constant.
    /// </summary>
    public string Code { get; }

    public DataListCategoryAttribute(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
    }
}
