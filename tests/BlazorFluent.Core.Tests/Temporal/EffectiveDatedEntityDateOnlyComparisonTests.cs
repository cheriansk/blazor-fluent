using BlazorFluent.Core.Domain.Base;
using BlazorFluent.Core.Domain.Identity;
using BlazorFluent.Core.Domain.Tenancy;
using BlazorFluent.Core.Domain.Calendar;
using BlazorFluent.Core.Domain.Calendar.Validators;
using BlazorFluent.Core.Extensions;
using BlazorFluent.Shared.DataListTypes;
using FluentAssertions;
using Xunit;

namespace BlazorFluent.Core.Tests.Temporal;

public class EffectiveDatedEntityDateOnlyComparisonTests
{
    private const string SecretKey = "test_cryptographic_secret_key_minimum_32_bytes_long!";

    [Fact]
    public void UserEntity_Implements_IEffectiveDatedEntity()
    {
        var user = new UserEntity
        {
            Email = "test@example.com",
            FullName = "Test User",
            UserType = UserTypeEnum.CompanyUser,
            TimeZoneId = "UTC",
            StartDate = new DateTime(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 10, 19, 23, 59, 59, DateTimeKind.Utc)
        };

        user.Should().BeAssignableTo<IEffectiveDatedEntity>();
        var effectiveDated = (IEffectiveDatedEntity)user;
        effectiveDated.StartDate.Should().Be(user.StartDate);
        effectiveDated.EndDate.Should().Be(user.EndDate);
    }

    [Fact]
    public void ProjectCalendarEntity_Implements_IEffectiveDatedEntity()
    {
        var milestone = new ProjectCalendarEntity
        {
            ProjectId = Guid.NewGuid(),
            Title = "Sprint 1", EntryType = CalendarEntryTypeEnum.Milestone,
            Status = CalendarEntryStatusEnum.InProgress,
            StartDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc)
        };

        milestone.Should().BeAssignableTo<IEffectiveDatedEntity>();
        var effectiveDated = (IEffectiveDatedEntity)milestone;
        effectiveDated.StartDate.Should().Be(milestone.StartDate);
        effectiveDated.EndDate.Should().Be(milestone.EndDate);
    }

    [Fact]
    public void IsActiveAt_ComparesByDate_RemainsActiveThroughoutEntireDayOfEndDate()
    {
        // Arrange: EndDate is Oct 19 at 00:00:00 UTC (start of day)
        var milestone = new ProjectCalendarEntity
        {
            ProjectId = Guid.NewGuid(),
            Title = "Milestone Alpha", EntryType = CalendarEntryTypeEnum.Milestone,
            Status = CalendarEntryStatusEnum.InProgress,
            StartDate = new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc)
        };

        // Act: Evaluate at 16:30:00 UTC on Oct 19 (afternoon of the end date)
        var evalTime = new DateTime(2026, 10, 19, 16, 30, 0, DateTimeKind.Utc);
        var isActive = ((IEffectiveDatedEntity)milestone).IsActiveAt(evalTime);

        // Assert: Must be active throughout the entire calendar day
        isActive.Should().BeTrue();

        // On the next calendar day (Oct 20 at 00:00:01 UTC), it is now expired
        var nextDay = new DateTime(2026, 10, 20, 0, 0, 1, DateTimeKind.Utc);
        ((IEffectiveDatedEntity)milestone).IsActiveAt(nextDay).Should().BeFalse();
    }

    [Fact]
    public void IsValidForLogin_ComparesByDate_PermitsLoginThroughoutEntireDayOfEndDate()
    {
        var user = new UserEntity
        {
            Email = "contractor@example.com",
            FullName = "Charlie Contractor",
            UserType = UserTypeEnum.ClientUser,
            TimeZoneId = "UTC",
            IsActive = true,
            StartDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc)
        };
        user.RowSignature = user.ComputeIntegritySignature(SecretKey);

        // Attempt login on the afternoon of Oct 19 (the end date)
        var afternoonOnEndDate = new DateTime(2026, 10, 19, 17, 45, 0, DateTimeKind.Utc);
        var isValid = user.IsValidForLogin(afternoonOnEndDate, SecretKey, out var failureReason);

        isValid.Should().BeTrue();
        failureReason.Should().BeEmpty();

        // Attempt login on Oct 20 (expired)
        var nextDay = new DateTime(2026, 10, 20, 0, 0, 1, DateTimeKind.Utc);
        var isExpired = user.IsValidForLogin(nextDay, SecretKey, out var expireReason);

        isExpired.Should().BeFalse();
        expireReason.Should().Contain("expired");
    }

    [Fact]
    public void Validators_SameDayDates_PassValidation()
    {
        var userValidator = new UserEntityValidator();
        var sameDayUser = new UserEntity
        {
            Email = "sameday@example.com",
            FullName = "Same Day",
            UserType = UserTypeEnum.CompanyUser,
            TimeZoneId = "UTC",
            StartDate = new DateTime(2026, 10, 19, 14, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 10, 19, 9, 0, 0, DateTimeKind.Utc) // earlier time, same date
        };

        var userResult = userValidator.Validate(sameDayUser);
        userResult.IsValid.Should().BeTrue();

        var milestoneValidator = new ProjectCalendarEntityValidator();
        var sameDayMilestone = new ProjectCalendarEntity
        {
            ProjectId = Guid.NewGuid(),
            Title = "Single-Day Milestone", EntryType = CalendarEntryTypeEnum.Milestone,
            Status = CalendarEntryStatusEnum.Scheduled,
            StartDate = new DateTime(2026, 10, 19, 14, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 10, 19, 9, 0, 0, DateTimeKind.Utc) // earlier time, same date
        };

        var milestoneResult = milestoneValidator.Validate(sameDayMilestone);
        milestoneResult.IsValid.Should().BeTrue();
    }
}
