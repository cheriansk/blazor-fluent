namespace BlazorEnterprise.Shared.UI.Components.TimelineView;

public class TimelineMilestone
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string? Category { get; set; }
    public string Color { get; set; } = "primary"; // primary, info, warning, success, accent, secondary
    public bool IsCompleted { get; set; }
}

public class TimelinePhase
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Subtitle { get; set; } // e.g. "Budget $954" or "Sep 2 - Oct 26"
    public string? DateRangeText { get; set; }
    public string Color { get; set; } = "primary"; // primary, info, warning, accent, success
    public double? ProgressPercent { get; set; }
    public object? Tag { get; set; }
}
