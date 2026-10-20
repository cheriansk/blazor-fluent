using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Core.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorFluent.Persistence.Configurations.Tasks;

public class SubTaskEntityConfiguration : IEntityTypeConfiguration<SubTaskEntity>
{
    public void Configure(EntityTypeBuilder<SubTaskEntity> builder)
    {
        builder.ToTable("SubTasks", EntitySchemas.tasks.ToString());

        builder.HasKey(s => s.Id);

        builder.Property(s => s.TenantId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(s => s.Level)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(TaskLevelEnum.Project);

        builder.Property(s => s.ParentTaskId)
            .IsRequired();

        builder.Property(s => s.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(s => s.Description)
            .HasMaxLength(10000)
            .IsRequired(false);

        builder.Property(s => s.OrderIndex)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(s => s.BlockedBySubTaskId)
            .IsRequired(false);

        builder.Property(s => s.Priority)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(s => s.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(s => s.StartDate)
            .IsRequired();

        builder.Property(s => s.DueDate)
            .IsRequired();

        builder.Property(s => s.EndDate)
            .IsRequired(false);

        builder.Property(s => s.ReporterEmail)
            .HasMaxLength(256)
            .IsRequired()
            .HasDefaultValue(string.Empty);

        builder.Property(s => s.AssigneeEmails)
            .HasMaxLength(2000)
            .IsRequired(false)
            .HasDefaultValue(string.Empty);

        builder.Property(s => s.WatcherEmails)
            .HasMaxLength(2000)
            .IsRequired(false)
            .HasDefaultValue(string.Empty);

        builder.Property(s => s.Labels)
            .HasMaxLength(500)
            .IsRequired(false)
            .HasDefaultValue(string.Empty);

        builder.Property(s => s.ProgramId)
            .IsRequired(false);

        builder.Property(s => s.ProjectId)
            .IsRequired(false);

        builder.Property(s => s.UserId)
            .HasMaxLength(256)
            .IsRequired(false);

        // Soft delete properties
        builder.Property(s => s.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(s => s.DeletedAtUtc)
            .IsRequired(false);

        builder.Property(s => s.DeletedBy)
            .HasMaxLength(256)
            .IsRequired(false);

        // Self-referencing intra-task blocker navigation
        builder.HasOne(s => s.BlockedBySubTask)
            .WithMany()
            .HasForeignKey(s => s.BlockedBySubTaskId)
            .OnDelete(DeleteBehavior.Restrict);

        // High performance indexes
        builder.HasIndex(s => s.TenantId)
            .HasDatabaseName("IX_SubTasks_TenantId");

        builder.HasIndex(s => s.ParentTaskId)
            .HasDatabaseName("IX_SubTasks_ParentTaskId");

        builder.HasIndex(s => new { s.ParentTaskId, s.OrderIndex })
            .HasDatabaseName("IX_SubTasks_ParentTask_Order");

        builder.HasIndex(s => new { s.TenantId, s.Status, s.DueDate })
            .HasDatabaseName("IX_SubTasks_Tenant_Status_DueDate");
    }
}
