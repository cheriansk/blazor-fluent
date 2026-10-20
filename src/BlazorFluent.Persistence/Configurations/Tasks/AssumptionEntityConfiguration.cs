using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Core.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorFluent.Persistence.Configurations.Tasks;

public class AssumptionEntityConfiguration : IEntityTypeConfiguration<AssumptionEntity>
{
    public void Configure(EntityTypeBuilder<AssumptionEntity> builder)
    {
        builder.ToTable("Assumptions", EntitySchemas.tasks.ToString());

        builder.HasKey(a => a.Id);

        builder.Property(a => a.TenantId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(a => a.Level)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(TaskLevelEnum.Project);

        builder.Property(a => a.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(a => a.Description)
            .HasMaxLength(10000)
            .IsRequired(false);

        builder.Property(a => a.ValidationCriteria)
            .HasMaxLength(5000)
            .IsRequired(false)
            .HasDefaultValue(string.Empty);

        builder.Property(a => a.ValidationStatus)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(a => a.ImpactIfInvalid)
            .HasMaxLength(5000)
            .IsRequired(false)
            .HasDefaultValue(string.Empty);

        builder.Property(a => a.Priority)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(a => a.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(a => a.StartDate)
            .IsRequired();

        builder.Property(a => a.DueDate)
            .IsRequired();

        builder.Property(a => a.EndDate)
            .IsRequired(false);

        builder.Property(a => a.ReporterEmail)
            .HasMaxLength(256)
            .IsRequired()
            .HasDefaultValue(string.Empty);

        builder.Property(a => a.AssigneeEmails)
            .HasMaxLength(2000)
            .IsRequired(false)
            .HasDefaultValue(string.Empty);

        builder.Property(a => a.WatcherEmails)
            .HasMaxLength(2000)
            .IsRequired(false)
            .HasDefaultValue(string.Empty);

        builder.Property(a => a.Labels)
            .HasMaxLength(500)
            .IsRequired(false)
            .HasDefaultValue(string.Empty);

        builder.Property(a => a.ProgramId)
            .IsRequired(false);

        builder.Property(a => a.ProjectId)
            .IsRequired(false);

        builder.Property(a => a.MilestoneId)
            .IsRequired(false);

        builder.Property(a => a.UserId)
            .HasMaxLength(256)
            .IsRequired(false);

        // Soft delete properties
        builder.Property(a => a.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(a => a.DeletedAtUtc)
            .IsRequired(false);

        builder.Property(a => a.DeletedBy)
            .HasMaxLength(256)
            .IsRequired(false);

        // Targeted High-Performance Compound Indexes
        builder.HasIndex(a => a.TenantId)
            .HasDatabaseName("IX_Assumptions_TenantId");

        builder.HasIndex(a => new { a.TenantId, a.Level })
            .HasDatabaseName("IX_Assumptions_TenantId_Level");

        builder.HasIndex(a => new { a.TenantId, a.ProgramId })
            .HasDatabaseName("IX_Assumptions_TenantId_ProgramId");

        builder.HasIndex(a => new { a.TenantId, a.ProjectId })
            .HasDatabaseName("IX_Assumptions_TenantId_ProjectId");

        builder.HasIndex(a => new { a.TenantId, a.Level, a.ValidationStatus })
            .HasDatabaseName("IX_Assumptions_Tenant_Level_ValidationStatus");
    }
}
