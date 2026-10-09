using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Tasks;
using BlazorFluent.Core.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorFluent.Persistence.Configurations.Tasks;

public class TaskEntityConfiguration : IEntityTypeConfiguration<TaskEntity>
{
    public void Configure(EntityTypeBuilder<TaskEntity> builder)
    {
        builder.ToTable("Tasks", EntitySchemas.tasks.ToString());

        builder.HasKey(t => t.Id);

        builder.Property(t => t.TenantId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(t => t.Level)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(TaskLevel.Project);

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

        builder.Property(t => t.EndDate)
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
            .IsRequired(false);

        builder.Property(t => t.UserId)
            .HasMaxLength(256)
            .IsRequired(false);

        builder.Property(t => t.MilestoneId)
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

        builder.HasOne<ProjectMilestoneEntity>()
            .WithMany()
            .HasForeignKey(t => t.MilestoneId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(t => t.SubTasks)
            .WithOne(s => s.ParentTask)
            .HasForeignKey(s => s.ParentTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.OutgoingLinks)
            .WithOne(l => l.SourceTask)
            .HasForeignKey(l => l.SourceTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.IncomingLinks)
            .WithOne(l => l.TargetTask)
            .HasForeignKey(l => l.TargetTaskId)
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
    }
}
