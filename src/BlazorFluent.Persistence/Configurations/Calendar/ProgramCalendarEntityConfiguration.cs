using BlazorFluent.Core.Domain.Calendar;
using BlazorFluent.Core.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorFluent.Persistence.Configurations.Calendar;

public class ProgramCalendarEntityConfiguration : IEntityTypeConfiguration<ProgramCalendarEntity>
{
    public void Configure(EntityTypeBuilder<ProgramCalendarEntity> builder)
    {
        builder.ToTable("ProgramCalendars", EntitySchemas.calendar.ToString());

        builder.HasKey(c => c.Id);

        builder.Property(c => c.TenantId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(c => c.ProgramId)
            .IsRequired();

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

        builder.Property(c => c.EntryType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(c => c.Status)
            .IsRequired()
            .HasConversion<int>();

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

        // Relationship to Program
        builder.HasOne(c => c.Program)
            .WithMany()
            .HasForeignKey(c => c.ProgramId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(c => c.TenantId)
            .HasDatabaseName("IX_ProgramCalendars_TenantId");

        builder.HasIndex(c => new { c.TenantId, c.ProgramId })
            .HasDatabaseName("IX_ProgramCalendars_Tenant_Program");

        builder.HasIndex(c => new { c.ProgramId, c.StartDate, c.EndDate })
            .HasDatabaseName("IX_ProgramCalendars_Program_Start_End");
    }
}
