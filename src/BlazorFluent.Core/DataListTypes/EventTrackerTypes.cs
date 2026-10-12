using BlazorFluent.Core.Attributes;
using System.ComponentModel.DataAnnotations;

namespace BlazorFluent.Core.DataListTypes;

public enum EventTrackerStatus
{
    [Display(Name = "Published", Description = "Event has been dispatched and is awaiting processing")]
    [DataListCategory(EventTrackerDefinitions.Categories.Lifecycle)]
    Published = 1,

    [Display(Name = "In Progress", Description = "Event is currently being processed by one or more consumers")]
    [DataListCategory(EventTrackerDefinitions.Categories.Lifecycle)]
    InProgress = 2,

    [Display(Name = "Completed", Description = "All registered consumers have completed processing successfully")]
    [DataListCategory(EventTrackerDefinitions.Categories.Lifecycle)]
    Completed = 3,

    [Display(Name = "Partially Failed", Description = "One or more consumers completed while others failed")]
    [DataListCategory(EventTrackerDefinitions.Categories.Lifecycle)]
    PartiallyFailed = 4,

    [Display(Name = "Failed", Description = "All consumers failed or a fatal processing error occurred")]
    [DataListCategory(EventTrackerDefinitions.Categories.Lifecycle)]
    Failed = 5
}

public enum EventConsumptionStatus
{
    [Display(Name = "Running", Description = "Consumer is actively executing")]
    [DataListCategory(EventTrackerDefinitions.Categories.Execution)]
    Running = 1,

    [Display(Name = "Succeeded", Description = "Consumer completed execution successfully")]
    [DataListCategory(EventTrackerDefinitions.Categories.Execution)]
    Succeeded = 2,

    [Display(Name = "Failed", Description = "Consumer threw an unhandled exception or failed")]
    [DataListCategory(EventTrackerDefinitions.Categories.Execution)]
    Failed = 3,

    [Display(Name = "Retrying", Description = "Consumer failed and is awaiting a scheduled retry attempt")]
    [DataListCategory(EventTrackerDefinitions.Categories.Execution)]
    Retrying = 4
}

public static class EventTrackerDefinitions
{
    public static class Categories
    {
        [Display(Name = "Event Lifecycle", Description = "Overall published event processing lifecycle")]
        public const string Lifecycle = "Lifecycle";

        [Display(Name = "Consumer Execution", Description = "Individual consumer execution telemetry")]
        public const string Execution = "Execution";
    }
}
