using BlazorFluent.Core.Domain.Calendar;
using BlazorFluent.Core.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorFluent.Persistence.Configurations.Calendar;

public class UserCalendarEntityConfiguration : IEntityTypeConfiguration<UserCalendarEntity>
{
    public void Configure(EntityTypeBuilder<UserCalendarEntity> builder)
    {
        builder.ToTable("UserCalendars", EntitySchemas.calendar.ToString());

        builder.HasKey(c => c.Id);

        builder.Property(c => c.TenantId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(c => c.UserId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(c => c.UserEntityId)
            .IsRequired(false);

        builder.Property(c => c.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.Description)
            .HasMaxLength(5000)
            .IsRequired(false)
            .HasDefaultValue(string.Empty);

        builder.Property(c => c.StartUtc)
            .IsRequired();

        builder.Property(c => c.EndUtc)
            .IsRequired();

        builder.Ignore(c => c.StartDate);
        builder.Ignore(c => c.EndDate);

        builder.Property(c => c.IsAllDay)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(c => c.EntryType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(c => c.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(c => c.OrganizerEmail)
            .HasMaxLength(256)
            .IsRequired()
            .HasDefaultValue(string.Empty);

        builder.Property(c => c.ParticipantEmails)
            .HasMaxLength(2000)
            .IsRequired(false)
            .HasDefaultValue(string.Empty);

        builder.Property(c => c.LocationOrMeetingUrl)
            .HasMaxLength(2000)
            .IsRequired(false);

        builder.Property(c => c.AssociatedTaskId)
            .IsRequired(false);

        // Soft delete properties
        builder.Property(c => c.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(c => c.DeletedAtUtc)
            .IsRequired(false);

        builder.Property(c => c.DeletedBy)
            .HasMaxLength(256)
            .IsRequired(false);

        // Optional relationship to UserEntity
        builder.HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserEntityId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(c => c.TenantId)
            .HasDatabaseName("IX_UserCalendars_TenantId");

        builder.HasIndex(c => new { c.TenantId, c.UserId })
            .HasDatabaseName("IX_UserCalendars_Tenant_User");

        builder.HasIndex(c => new { c.UserId, c.StartUtc, c.EndUtc })
            .HasDatabaseName("IX_UserCalendars_User_Start_End");
    }
}
