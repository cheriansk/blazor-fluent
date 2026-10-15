using BlazorFluent.Shared.Contracts.Engine.Calendar;

namespace BlazorFluent.RulesEngine.Calendar;

/// <summary>
/// Working day calculation engine.
/// Evaluates duration offsets excluding weekends and dynamic tenant holidays.
/// </summary>
public static class CalendarWorkingDayEngine
{
    public static CalendarScheduleRespDto Calculate(CalendarScheduleReqDto request)
    {
        var holidaySet = new HashSet<DateTime>(
            request.KnownHolidaysUtc.Select(h => h.Date)
        );

        var currentDate = request.StartDateUtc.Date;
        var workingDaysCount = 0;
        var nonWorkingDaysCount = 0;

        // Advance date until target duration of working days is satisfied
        while (workingDaysCount < request.DurationDays)
        {
            var isWeekend = request.ExcludeWeekends &&
                (currentDate.DayOfWeek == DayOfWeek.Saturday || currentDate.DayOfWeek == DayOfWeek.Sunday);

            var isHoliday = holidaySet.Contains(currentDate);

            if (isWeekend || isHoliday)
            {
                nonWorkingDaysCount++;
            }
            else
            {
                workingDaysCount++;
            }

            if (workingDaysCount < request.DurationDays)
            {
                currentDate = currentDate.AddDays(1);
            }
        }

        return new CalendarScheduleRespDto(
            AdjustedEndDateUtc: DateTime.SpecifyKind(currentDate, DateTimeKind.Utc),
            WorkingDaysCount: workingDaysCount,
            NonWorkingDaysCount: nonWorkingDaysCount
        );
    }
}
