using BlazorEnterprise.Core.Domain.Base;
using BlazorEnterprise.Core.Domain.Identity;

namespace BlazorEnterprise.Core.Domain.Calendar;

/// <summary>
/// Dedicated user-level personal calendar entry.
/// Mapped to physical table 'user_calendars'.
/// Tracks personal appointments, working focus sessions, and planned time off (PTO/Vacation/Out of Office).
/// Implements IUnversionedEntity since user schedules and time off do not require multi-draft approval workflows.
/// </summary>
public class UserCalendarEntity : BaseCalendarEntity, IUnversionedEntity
{
    /// <summary>
    /// Foreign key identifier of the UserEntity owning this personal calendar entry.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Mandatory due date for personal calendar entries.
    /// </summary>
    public DateTime DueDate { get; set; }

    /// <summary>
    /// Visibility flag.
    /// When true (default for Personal), only visible to the owning user.
    /// When false (default for OutOfOffice/PTO/Vacation), visible to team capacity/sprint planners.
    /// </summary>
    public bool IsPrivate { get; set; }

    // --- Navigation Property ---
    public UserEntity? User { get; set; }
}
