using BlazorEnterprise.Core.Domain.Tasks;
using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorEnterprise.Persistence.Configurations.Tasks;

public class WorkDependencyConfiguration : IEntityTypeConfiguration<WorkDependencyEntity>
{
    public void Configure(EntityTypeBuilder<WorkDependencyEntity> builder)
    {
        builder.ToTable("WorkDependencies", EntitySchemas.tasks.ToString());

        builder.HasKey(w => w.Id);

        builder.Property(w => w.TenantId)
            .HasColumnOrder(0)
            .IsRequired();

        builder.Property(w => w.Level)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(TaskLevelEnum.Project);

        // Ignore [NotMapped] edge relations and live-resolved metadata (3NF pure)
        builder.Property(w => w.ProgramId).IsRequired(false);
        builder.Property(w => w.ProjectId).IsRequired(false);
        builder.Ignore(w => w.MilestoneId);
        builder.Ignore(w => w.DependsOnLevel);
        builder.Ignore(w => w.DependsOnProjectId);
        builder.Ignore(w => w.DependsOnProgramId);
        builder.Ignore(w => w.DependsOnSnapshotTitle);
        builder.Ignore(w => w.DependsOnSnapshotStatus);
        builder.Ignore(w => w.DependsOnSnapshotDueDate);

        // Side B: Dependent / Successor
        builder.Property(w => w.DependentType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(w => w.DependentItemId)
            .IsRequired(false);

        // Side A: Prerequisite / Predecessor
        builder.Property(w => w.DependsOnType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(w => w.DependsOnItemId)
            .IsRequired(false);

        builder.Property(w => w.IsScheduleConflict)
            .IsRequired()
            .HasDefaultValue(false);

        // Dependency & RAID Metadata
        builder.Property(w => w.DependencyType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(w => w.Title)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(w => w.Description)
            .HasMaxLength(10000)
            .IsRequired(false);

        builder.Property(w => w.Priority)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(w => w.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(w => w.StartDate)
            .IsRequired();

        builder.Property(w => w.DueDate)
            .IsRequired();

        builder.Property(w => w.RevisedDueDate)
            .IsRequired(false);

        builder.Property(w => w.EndDate)
            .IsRequired(false);

        builder.Property(w => w.ActualStartDate)
            .IsRequired(false);

        builder.Property(w => w.ActualEndDate)
            .IsRequired(false);

        builder.Property(w => w.ReporterEmail)
            .HasMaxLength(256)
            .IsRequired()
            .HasDefaultValue(string.Empty);

        builder.Property(w => w.AssigneeEmails)
            .HasMaxLength(2000)
            .IsRequired(false)
            .HasDefaultValue(string.Empty);

        builder.Property(w => w.WatcherEmails)
            .HasMaxLength(2000)
            .IsRequired(false)
            .HasDefaultValue(string.Empty);

        builder.Property(w => w.Labels)
            .HasMaxLength(500)
            .IsRequired(false)
            .HasDefaultValue(string.Empty);

        // Soft delete properties
        builder.Property(w => w.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(w => w.DeletedAtUtc)
            .IsRequired(false);

        builder.Property(w => w.DeletedBy)
            .HasMaxLength(256)
            .IsRequired(false);

        // Targeted High-Performance Compound Indexes
        builder.HasIndex(w => w.TenantId)
            .HasDatabaseName("IX_WorkDependencies_TenantId");

        builder.HasIndex(w => new { w.TenantId, w.DependentType, w.DependentItemId })
            .HasDatabaseName("IX_WorkDependencies_Tenant_DependentItem");

        builder.HasIndex(w => new { w.TenantId, w.DependsOnType, w.DependsOnItemId })
            .HasDatabaseName("IX_WorkDependencies_Tenant_DependsOnItem");
    }
}
