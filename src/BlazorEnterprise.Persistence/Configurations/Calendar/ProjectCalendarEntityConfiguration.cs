using BlazorEnterprise.Core.Domain.Calendar;
using BlazorEnterprise.Core.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorEnterprise.Persistence.Configurations.Calendar;

public class ProjectCalendarEntityConfiguration : IEntityTypeConfiguration<ProjectCalendarEntity>
{
    public void Configure(EntityTypeBuilder<ProjectCalendarEntity> builder)
    {
        builder.ToTable("ProjectCalendars", EntitySchemas.calendar.ToString());

        builder.HasKey(c => c.Id);

        builder.Property(c => c.TenantId)
            .IsRequired();

        builder.Property(c => c.ProjectId)
            .IsRequired();

        builder.Property(c => c.ProgramId)
            .IsRequired(false);

        // IVersionedEntity Properties
        builder.Property(c => c.RevisionHeaderId)
            .IsRequired();

        builder.Property(c => c.RelId)
            .IsRequired();

        builder.Property(c => c.VerNum)
            .IsRequired();

        builder.Property(c => c.ParentRowId)
            .IsRequired()
            .HasDefaultValue(Guid.Empty);

        builder.Property(c => c.ParentProgramCalendarId)
            .IsRequired(false);

        builder.Property(c => c.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.Description)
            .HasMaxLength(5000)
            .IsRequired(false)
            .HasDefaultValue(string.Empty);

        builder.Property(c => c.StartDate)
            .IsRequired();

        builder.Property(c => c.EndDate)
            .IsRequired(false);

        builder.Property(c => c.OwnerEmails)
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(c => c.ParentTimelineId)
            .IsRequired(false);

        builder.Property(c => c.EntryType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(c => c.Status)
            .IsRequired()
            .HasConversion<int>();

        // Soft delete properties
        builder.Property(c => c.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(c => c.DeletedAtUtc)
            .IsRequired(false);

        builder.Property(c => c.DeletedBy)
            .HasMaxLength(256)
            .IsRequired(false);

        // Relationships (Restrict delete on RevisionHeader to prevent accidental cascade baseline loss)
        builder.HasOne(c => c.RevisionHeader)
            .WithMany()
            .HasForeignKey(c => c.RevisionHeaderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Project)
            .WithMany()
            .HasForeignKey(c => c.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Program)
            .WithMany()
            .HasForeignKey(c => c.ProgramId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(c => c.ParentProgramCalendar)
            .WithMany()
            .HasForeignKey(c => c.ParentProgramCalendarId)
            .OnDelete(DeleteBehavior.SetNull);

        // Self-referencing relationship: Timeline row -> Milestones
        builder.HasOne(c => c.ParentTimeline)
            .WithMany(t => t.Milestones)
            .HasForeignKey(c => c.ParentTimelineId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(c => c.TenantId)
            .HasDatabaseName("IX_ProjectCalendars_TenantId");

        builder.HasIndex(c => new { c.TenantId, c.ProjectId })
            .HasDatabaseName("IX_ProjectCalendars_Tenant_Project");

        builder.HasIndex(c => c.RevisionHeaderId)
            .HasDatabaseName("IX_ProjectCalendars_RevisionHeaderId");

        builder.HasIndex(c => c.RelId)
            .HasDatabaseName("IX_ProjectCalendars_RelId");

        builder.HasIndex(c => new { c.RevisionHeaderId, c.RelId })
            .IsUnique()
            .HasDatabaseName("IX_ProjectCalendars_Revision_RelId");

        builder.HasIndex(c => new { c.ProjectId, c.StartDate, c.EndDate })
            .HasDatabaseName("IX_ProjectCalendars_Project_Start_End");

        builder.HasIndex(c => c.ParentTimelineId)
            .HasDatabaseName("IX_ProjectCalendars_ParentTimelineId");
    }
}
