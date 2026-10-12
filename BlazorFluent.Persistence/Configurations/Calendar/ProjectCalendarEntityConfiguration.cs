using BlazorFluent.Core.Domain.Calendar;
using BlazorFluent.Core.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorFluent.Persistence.Configurations.Calendar;

public class ProjectCalendarEntityConfiguration : IEntityTypeConfiguration<ProjectCalendarEntity>
{
    public void Configure(EntityTypeBuilder<ProjectCalendarEntity> builder)
    {
        builder.ToTable("ProjectCalendars", EntitySchemas.calendar.ToString());

        builder.HasKey(c => c.Id);

        builder.Property(c => c.TenantId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(c => c.ProjectId)
            .IsRequired();

        builder.Property(c => c.ProgramId)
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

        // Relationships
        builder.HasOne(c => c.Project)
            .WithMany()
            .HasForeignKey(c => c.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Program)
            .WithMany()
            .HasForeignKey(c => c.ProgramId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(c => c.TenantId)
            .HasDatabaseName("IX_ProjectCalendars_TenantId");

        builder.HasIndex(c => new { c.TenantId, c.ProjectId })
            .HasDatabaseName("IX_ProjectCalendars_Tenant_Project");

        builder.HasIndex(c => new { c.ProjectId, c.StartUtc, c.EndUtc })
            .HasDatabaseName("IX_ProjectCalendars_Project_Start_End");
    }
}
