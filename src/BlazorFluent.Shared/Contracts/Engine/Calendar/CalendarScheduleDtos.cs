namespace BlazorFluent.Shared.Contracts.Engine.Calendar;

public record CalendarScheduleReqDto(
    DateTime StartDateUtc,
    int DurationDays,
    IReadOnlyList<DateTime> KnownHolidaysUtc,
    bool ExcludeWeekends = true
);

public record CalendarScheduleRespDto(
    DateTime AdjustedEndDateUtc,
    int WorkingDaysCount,
    int NonWorkingDaysCount
);
