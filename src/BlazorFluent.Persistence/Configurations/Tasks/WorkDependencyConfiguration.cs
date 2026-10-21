using BlazorFluent.Core.Domain.Tasks;
using BlazorFluent.Persistence.Configurations;
using BlazorFluent.Shared.DataListTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorFluent.Persistence.Configurations.Tasks;

public class WorkDependencyConfiguration : IEntityTypeConfiguration<WorkDependencyEntity>
{
    public void Configure(EntityTypeBuilder<WorkDependencyEntity> builder)
    {
        builder.ToTable("WorkDependencies", EntitySchemas.tasks.ToString());

        builder.HasKey(w => w.Id);

        builder.Property(w => w.TenantId)
            .IsRequired();

        builder.Property(w => w.Level)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(TaskLevelEnum.Project);

        builder.Property(w => w.ProgramId)
            .IsRequired(false);

        builder.Property(w => w.ProjectId)
            .IsRequired(false);

        builder.Property(w => w.UserId)
            .HasMaxLength(256)
            .IsRequired(false);

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

        builder.Property(w => w.DependsOnLevel)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(TaskLevelEnum.Project);

        builder.Property(w => w.DependsOnProjectId)
            .IsRequired(false);

        builder.Property(w => w.DependsOnProgramId)
            .IsRequired(false);

        // Cached snapshot of Side A
        builder.Property(w => w.DependsOnSnapshotTitle)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(w => w.DependsOnSnapshotStatus)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(w => w.DependsOnSnapshotDueDate)
            .IsRequired(false);

        builder.Property(w => w.IsScheduleConflict)
            .IsRequired()
            .HasDefaultValue(false);

        // Dependency & RAID Metadata
        builder.Property(w => w.DependencyType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(w => w.MilestoneId)
            .IsRequired(false);

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

        builderbuilder.Property(w => w.RevisedDueDate)
            .IsRequired(false);

        .Property(w => w.EndDate)
            .IsRequired(false);

        builder.Property(w => w.ActualStartDate)
            .IsRequired(false);

        builder.Property(w => w.ActualEndDate)
            .IsRequired(false);

        builder.Property(w => w.ReporterEmail)
            .HasMaxLength(256)
            .IsRequired(false)
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

        builder.Property(w => w.SourceRowId)
            .IsRequired(false);

        builder.Property(w => w.IsEncryptedAtRest)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(w => w.EncryptedPayloadBytes)
            .IsRequired(false);

        // Soft delete properties
        builder.Property(w => w.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(w => w.DeletedAtUtc)
            .IsRequired(false);

        builder.Property(w => w.DeletedBy)
            .HasMaxLength(256)
            .IsRequired(false);

        // Unmapped convenience aliases
        builder.Ignore(w => w.ImpactDetails);
        builder.Ignore(w => w.Criticality);
        builder.Ignore(w => w.ResolveDueDate);
        builder.Ignore(w => w.SourceTaskId);
        builder.Ignore(w => w.TargetTaskId);

        // Targeted High-Performance Compound Indexes
        builder.HasIndex(w => w.TenantId)
            .HasDatabaseName("IX_WorkDependencies_TenantId");

        builder.HasIndex(w => new { w.TenantId, w.DependentType, w.DependentItemId })
            .HasDatabaseName("IX_WorkDependencies_Tenant_DependentItem");

        builder.HasIndex(w => new { w.TenantId, w.DependsOnType, w.DependsOnItemId })
            .HasDatabaseName("IX_WorkDependencies_Tenant_DependsOnItem");

        builder.HasIndex(w => new { w.TenantId, w.ProjectId })
            .HasDatabaseName("IX_WorkDependencies_Tenant_ProjectId");

        builder.HasIndex(w => new { w.TenantId, w.ProgramId })
            .HasDatabaseName("IX_WorkDependencies_Tenant_ProgramId");

        builder.HasIndex(w => new { w.TenantId, w.DependsOnProjectId })
            .HasDatabaseName("IX_WorkDependencies_Tenant_DependsOnProjectId");

        builder.HasIndex(w => new { w.TenantId, w.DependsOnProgramId })
            .HasDatabaseName("IX_WorkDependencies_Tenant_DependsOnProgramId");

        builder.HasIndex(w => new { w.TenantId, w.IsScheduleConflict })
            .HasDatabaseName("IX_WorkDependencies_Tenant_ScheduleConflict");

        builder.HasIndex(w => new { w.TenantId, w.Status, w.DueDate })
            .HasDatabaseName("IX_WorkDependencies_Tenant_Status_DueDate");
    }
}
