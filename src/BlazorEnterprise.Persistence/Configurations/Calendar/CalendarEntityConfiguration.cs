using BlazorEnterprise.Shared.DataListTypes;
using BlazorEnterprise.Core.Domain.Calendar;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorEnterprise.Persistence.Configurations.Calendar;

public class CalendarEntityConfiguration : IEntityTypeConfiguration<CalendarEntity>
{
    public void Configure(EntityTypeBuilder<CalendarEntity> builder)
    {
        builder.ToTable("calendars", EntitySchemas.calendar.ToString());

        builder.HasKey(c => c.Id);

        builder.Property(c => c.TenantId)
            .HasColumnOrder(0)
            .IsRequired();

        builder.Property(c => c.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.Description)
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(c => c.OwnerEmails)
            .HasMaxLength(2000)
            .IsRequired(false)
            .HasDefaultValue(string.Empty);

        builder.Property(c => c.ParentTimelineId)
            .IsRequired(false);

        builder.Property(c => c.StartDate)
            .IsRequired();

        builder.Property(c => c.EndDate)
            .IsRequired(false);

        builder.Property(c => c.EntryType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(c => c.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(c => c.Level)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(TaskLevelEnum.Project);

        builder.Property(c => c.ParentProgramCalendarId)
            .IsRequired(false);

        // Versioning properties (IVersionedEntity)
        builder.Property(c => c.RevisionHeaderId)
            .IsRequired();

        builder.Property(c => c.RelId)
            .IsRequired();

        builder.Property(c => c.VerNum)
            .IsRequired();

        builder.Property(c => c.ParentRowId)
            .IsRequired()
            .HasDefaultValue(Guid.Empty);

        // Soft delete properties
        builder.Property(c => c.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(c => c.DeletedAtUtc)
            .IsRequired(false);

        builder.Property(c => c.DeletedBy)
            .HasMaxLength(256)
            .IsRequired(false);

        // Ignore [NotMapped] edge relations
        builder.Property(c => c.ProgramId).IsRequired(false);
        builder.Property(c => c.ProjectId).IsRequired(false);

        // Relationships
        builder.HasOne(c => c.RevisionHeader)
            .WithMany()
            .HasForeignKey(c => c.RevisionHeaderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.ParentTimeline)
            .WithMany(c => c.Milestones)
            .HasForeignKey(c => c.ParentTimelineId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(c => c.TenantId)
            .HasDatabaseName("IX_Calendars_TenantId");

        builder.HasIndex(c => new { c.TenantId, c.ProgramId })
            .HasDatabaseName("IX_Calendars_Tenant_Program");
        builder.HasIndex(c => new { c.TenantId, c.ProjectId })
            .HasDatabaseName("IX_Calendars_Tenant_Project");
        builder.HasIndex(c => c.RevisionHeaderId)
            .HasDatabaseName("IX_Calendars_RevisionHeaderId");

        builder.HasIndex(c => c.RelId)
            .HasDatabaseName("IX_Calendars_RelId");

        builder.HasIndex(c => new { c.RevisionHeaderId, c.RelId })
            .IsUnique()
            .HasDatabaseName("IX_Calendars_Revision_RelId");
    }
}
