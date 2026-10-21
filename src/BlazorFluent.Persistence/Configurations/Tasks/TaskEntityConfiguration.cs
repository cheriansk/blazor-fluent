using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Core.Domain.Tasks;
using BlazorFluent.Core.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorFluent.Persistence.Configurations.Tasks;

public partial class TaskEntityConfiguration : IEntityTypeConfiguration<TaskEntity>
{
    public void Configure(EntityTypeBuilder<TaskEntity> builder)
    {
        builder.ToTable("Tasks", EntitySchemas.tasks.ToString(), t =>
        {
            t.HasCheckConstraint(
                "CK_Tasks_WorkflowId_WorkflowStepOrder",
                "(\"WorkflowId\" IS NULL AND \"WorkflowStepOrder\" IS NULL) OR (\"WorkflowId\" IS NOT NULL AND \"WorkflowStepOrder\" IS NOT NULL AND \"WorkflowStepOrder\" > 0)");
        });

        builder.HasKey(t => t.Id);

        builder.Property(t => t.TenantId)
            .IsRequired();

        builder.Property(t => t.Level)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(TaskLevelEnum.Project);

        builder.Property(t => t.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.Description)
            .HasMaxLength(10000)
            .IsRequired(false);

        builder.Property(t => t.Priority)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(t => t.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(t => t.StartDate)
            .IsRequired();

        builder.Property(t => t.DueDate)
            .IsRequired();

        builder.Property(t => t.RevisedDueDate)
            .IsRequired(false);

        builder.Property(t => t.EndDate)
            .IsRequired(false);

        builder.Property(t => t.ActualStartDate)
            .IsRequired(false);

        builder.Property(t => t.ActualEndDate)
            .IsRequired(false);

        builder.Property(t => t.ReporterEmail)
            .HasMaxLength(256)
            .IsRequired()
            .HasDefaultValue(string.Empty);

        builder.Property(t => t.AssigneeEmails)
            .HasMaxLength(2000)
            .IsRequired(false)
            .HasDefaultValue(string.Empty);

        builder.Property(t => t.WatcherEmails)
            .HasMaxLength(2000)
            .IsRequired(false)
            .HasDefaultValue(string.Empty);

        builder.Property(t => t.Labels)
            .HasMaxLength(500)
            .IsRequired(false)
            .HasDefaultValue(string.Empty);

        builder.Property(t => t.ProgramId)
            .IsRequired(false);

        builder.Property(t => t.ProjectId)
            .IsRequired();

        builder.Property(t => t.UserId)
            .HasMaxLength(256)
            .IsRequired(false);

        builder.Property(t => t.MilestoneId)
            .IsRequired(false);

        builder.Property(t => t.WorkflowId)
            .IsRequired(false);

        builder.Property(t => t.WorkflowStepOrder)
            .IsRequired(false);

        // Soft delete properties
        builder.Property(t => t.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(t => t.DeletedAtUtc)
            .IsRequired(false);

        builder.Property(t => t.DeletedBy)
            .HasMaxLength(256)
            .IsRequired(false);

        // Relationships
        builder.HasOne<ProjectEntity>()
            .WithMany()
            .HasForeignKey(t => t.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ProgramEntity>()
            .WithMany()
            .HasForeignKey(t => t.ProgramId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Workflow)
            .WithMany(w => w.Steps)
            .HasForeignKey(t => t.WorkflowId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(t => t.SubTasks)
            .WithOne(s => s.ParentTask)
            .HasForeignKey(s => s.ParentTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        // Targeted High-Performance Compound Indexes
        builder.HasIndex(t => t.TenantId)
            .HasDatabaseName("IX_Tasks_TenantId");

        builder.HasIndex(t => new { t.TenantId, t.Level })
            .HasDatabaseName("IX_Tasks_TenantId_Level");

        builder.HasIndex(t => new { t.TenantId, t.ProjectId })
            .HasDatabaseName("IX_Tasks_TenantId_ProjectId");

        builder.HasIndex(t => new { t.TenantId, t.ProgramId })
            .HasDatabaseName("IX_Tasks_TenantId_ProgramId");

        builder.HasIndex(t => new { t.TenantId, t.ProjectId, t.Status, t.DueDate })
            .HasDatabaseName("IX_Tasks_Tenant_Project_Status_DueDate");

        builder.HasIndex(t => t.MilestoneId)
            .HasDatabaseName("IX_Tasks_MilestoneId");

        builder.HasIndex(t => new { t.TenantId, t.WorkflowId, t.WorkflowStepOrder })
            .HasDatabaseName("IX_Tasks_TenantId_WorkflowId_StepOrder");

        // Hook for downstream entity mapping customizations
        OnConfigureCustom(builder);
    }

    partial void OnConfigureCustom(EntityTypeBuilder<TaskEntity> builder);
}
