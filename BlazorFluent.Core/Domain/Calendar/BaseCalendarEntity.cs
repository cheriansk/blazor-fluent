using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Base;

namespace BlazorFluent.Core.Domain.Calendar;

/// <summary>
/// Abstract base class for all calendar entries across Program, Project, and User levels.
/// Tied to tenant via <see cref="TenantAuditableEntity"/>.
/// Implements <see cref="IEffectiveDatedEntity"/> and <see cref="ISoftDeletableEntity"/>.
/// </summary>
public abstract class BaseCalendarEntity : TenantAuditableEntity, IEffectiveDatedEntity, ISoftDeletableEntity
{
    /// <summary>
    /// Event title or headline. Required.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Event description, agenda, or notes.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Scheduled start date and time in UTC.
    /// </summary>
    public DateTime StartUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Scheduled end date and time in UTC.
    /// </summary>
    public DateTime EndUtc { get; set; } = DateTime.UtcNow.AddHours(1);

    /// <summary>
    /// IEffectiveDatedEntity contract: synchronized with StartUtc.
    /// </summary>
    public DateTime StartDate
    {
        get => StartUtc;
        set => StartUtc = value;
    }

    /// <summary>
    /// IEffectiveDatedEntity contract: synchronized with EndUtc.
    /// </summary>
    public DateTime? EndDate
    {
        get => EndUtc;
        set => EndUtc = value ?? StartUtc.AddHours(1);
    }

    /// <summary>
    /// Indicates whether the event spans the entire day.
    /// </summary>
    public bool IsAllDay { get; set; }

    /// <summary>
    /// Classification category of this calendar entry.
    /// </summary>
    public CalendarEntryType EntryType { get; set; } = CalendarEntryType.General;

    /// <summary>
    /// Current scheduling status (Scheduled, Confirmed, InProgress, Completed, Cancelled).
    /// </summary>
    public CalendarEntryStatus Status { get; set; } = CalendarEntryStatus.Scheduled;

    /// <summary>
    /// Email of the event organizer.
    /// </summary>
    public string OrganizerEmail { get; set; } = string.Empty;

    /// <summary>
    /// Semicolon-delimited list of invited participant emails.
    /// </summary>
    public string ParticipantEmails { get; set; } = string.Empty;

    /// <summary>
    /// Physical room location or virtual meeting URL (e.g. Teams, Zoom).
    /// </summary>
    public string? LocationOrMeetingUrl { get; set; }

    /// <summary>
    /// Optional foreign key to an associated work item (Task, Subtask, Risk, etc.).
    /// </summary>
    public Guid? AssociatedTaskId { get; set; }

    // --- ISoftDeletableEntity ---
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }

    public IReadOnlyList<string> GetParsedParticipantEmails() =>
        string.IsNullOrWhiteSpace(ParticipantEmails)
            ? []
            : ParticipantEmails.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
