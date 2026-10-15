using BlazorFluent.Shared.DataListTypes;

namespace BlazorFluent.Core.Domain.Tasks;

/// <summary>
/// Active impediment or operational issue ("I" of RAID).
/// Tracks realized problems actively hurting project or program delivery.
/// Can be scoped to Program or Project levels.
/// </summary>
public class IssueEntity : BaseTaskEntity
{
    /// <summary>
    /// Severity rating of this active impediment (Low, Medium, High, Critical).
    /// </summary>
    public IssueSeverity Severity { get; set; } = IssueSeverity.Medium;

    /// <summary>
    /// Investigation notes and identified root cause of this issue.
    /// </summary>
    public string RootCause { get; set; } = string.Empty;

    /// <summary>
    /// Temporary tactical workaround allowing partial progress while the permanent fix is developed.
    /// </summary>
    public string Workaround { get; set; } = string.Empty;

    /// <summary>
    /// Permanent resolution strategy and action plan.
    /// </summary>
    public string ResolutionStrategy { get; set; } = string.Empty;
}
