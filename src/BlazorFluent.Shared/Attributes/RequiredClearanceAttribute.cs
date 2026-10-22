using System;
using BlazorFluent.Shared.DataListTypes;

namespace BlazorFluent.Shared.Attributes;

/// <summary>
/// Declares the hierarchical clearance requirements for a Razor page, subcomponent, or API endpoint.
/// Allows specifying distinct tiers for viewing versus editing.
/// Evaluated at compile-time by Roslyn rule TG005 and at runtime by AuthorizedPageComponentBase.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public sealed class RequiredClearanceAttribute : Attribute
{
    /// <summary>
    /// Minimum clearance tier required to visit and view this resource.
    /// </summary>
    public ProjectRoleEnum ViewTier { get; }

    /// <summary>
    /// Minimum clearance tier required to edit, mutate, or submit actions on this resource.
    /// </summary>
    public ProjectRoleEnum EditTier { get; }

    /// <summary>
    /// Single-tier declaration:
    /// Resource is viewable by minTier and above.
    /// Resource is editable by Contribute (20) and above (or minTier if higher).
    /// </summary>
    public RequiredClearanceAttribute(ProjectRoleEnum minTier)
    {
        ViewTier = minTier;
        EditTier = (int)minTier < (int)ProjectRoleEnum.Contribute ? ProjectRoleEnum.Contribute : minTier;
    }

    /// <summary>
    /// Dual-tier declaration:
    /// Explicitly decouples view tier from edit tier (e.g. viewable by Lead, editable only by PMO).
    /// </summary>
    public RequiredClearanceAttribute(ProjectRoleEnum viewTier, ProjectRoleEnum editTier)
    {
        ViewTier = viewTier;
        EditTier = editTier;
    }
}
