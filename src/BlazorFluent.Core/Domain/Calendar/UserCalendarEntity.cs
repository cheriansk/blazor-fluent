using BlazorFluent.Core.Domain.Identity;

namespace BlazorFluent.Core.Domain.Calendar;

/// <summary>
/// Dedicated user-level personal calendar entry.
/// Mapped to physical table 'user_calendars'.
/// Tracks personal appointments, working focus sessions, and planned time off.
/// </summary>
public class UserCalendarEntity : BaseCalendarEntity
{
    /// <summary>
    /// User identifier owning this personal calendar entry.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Optional foreign key to UserEntity.
    /// </summary>
    public Guid? UserEntityId { get; set; }

    // --- Navigation Property ---
    public UserEntity? User { get; set; }
}
