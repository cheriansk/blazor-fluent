using BlazorFluent.Core.Domain.Calendar;
using BlazorFluent.Core.Domain.Calendar.Validators;
using BlazorFluent.Shared.DataListTypes;
using FluentAssertions;
using Xunit;

namespace BlazorFluent.Core.Tests.Calendar;

public class UserCalendarEntityValidatorTests
{
    private readonly UserCalendarEntityValidator _validator = new();

    [Fact]
    public void Validate_ValidUserCalendar_Passes()
    {
        var entry = new UserCalendarEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = "tenant_acme",
            UserId = Guid.CreateVersion7(),
            Title = "Focus Work Session",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddHours(2),
            EntryType = CalendarEntryType.WorkingSession,
            Status = CalendarEntryStatus.Confirmed
        };

        var result = _validator.Validate(entry);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_EmptyUserId_Fails()
    {
        var entry = new UserCalendarEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = "tenant_acme",
            UserId = Guid.Empty, // Invalid
            Title = "Focus Work Session",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddHours(2),
            EntryType = CalendarEntryType.WorkingSession,
            Status = CalendarEntryStatus.Scheduled
        };

        var result = _validator.Validate(entry);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UserCalendarEntity.UserId));
    }

    [Fact]
    public void Validate_MissingTitle_Fails()
    {
        var entry = new UserCalendarEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = "tenant_acme",
            UserId = Guid.CreateVersion7(),
            Title = "", // Invalid
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddHours(2),
            EntryType = CalendarEntryType.WorkingSession,
            Status = CalendarEntryStatus.Scheduled
        };

        var result = _validator.Validate(entry);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UserCalendarEntity.Title));
    }

    [Fact]
    public void Validate_EndDateBeforeStartDate_Fails()
    {
        var entry = new UserCalendarEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = "tenant_acme",
            UserId = Guid.CreateVersion7(),
            Title = "Sprint Review",
            StartDate = DateTime.UtcNow.AddHours(2),
            EndDate = DateTime.UtcNow, // Invalid: earlier than StartDate
            EntryType = CalendarEntryType.WorkingSession,
            Status = CalendarEntryStatus.Scheduled
        };

        var result = _validator.Validate(entry);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UserCalendarEntity.EndDate));
    }
}
