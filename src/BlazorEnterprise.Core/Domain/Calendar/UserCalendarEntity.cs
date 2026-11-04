using BlazorEnterprise.Core.Domain.Identity;

namespace BlazorEnterprise.Core.Domain.Calendar;

/// <summary>
/// Dedicated user-level personal calendar entry.
/// Mapped to physical table 'user_calendars'.
/// Tracks personal appointments, working focus sessions, and planned time off.
/// </summary>
public class UserCalendarEntity : BaseCalendarEntity
{
    /// <summary>
    /// Foreign key identifier of the UserEntity owning this personal calendar entry.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Mandatory due date for personal calendar entries.
    /// </summary>
    public DateTime DueDate { get; set; }

    // --- Navigation Property ---
    public UserEntity? User { get; set; }
}
