using BlazorFluent.Core.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorFluent.Persistence.Configurations.Tasks;

public class TaskEntityConfiguration : IEntityTypeConfiguration<TaskEntity>
{
    public void Configure(EntityTypeBuilder<TaskEntity> builder)
    {
        builder.ToTable("Tasks", EntitySchemas.app.ToString());

        builder.HasKey(t => t.Id);

        builder.Property(t => t.TenantId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(t => t.ProjectId)
            .IsRequired();

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

        builder.Property(t => t.DueDate)
            .IsRequired(false);

        builder.Property(t => t.AssigneeEmails)
            .HasMaxLength(2000)
            .IsRequired(false)
            .HasDefaultValue(string.Empty);

        builder.Property(t => t.Labels)
            .HasMaxLength(500)
            .IsRequired(false)
            .HasDefaultValue(string.Empty);

        builder.Property(t => t.IsClosed)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(t => t.ClosedAtUtc)
            .IsRequired(false);

        builder.Property(t => t.ClosedBy)
            .HasMaxLength(256)
            .IsRequired(false);

        // Soft delete properties
        builder.Property(t => t.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(t => t.DeletedBy)
            .HasMaxLength(256)
            .IsRequired(false);

        // Relationship: Task belongs to Project
        builder.HasOne(t => t.Project)
            .WithMany()
            .HasForeignKey(t => t.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        // Relationship: Task has many Comments
        builder.HasMany(t => t.Comments)
            .WithOne(c => c.Task)
            .HasForeignKey(c => c.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for high-performance querying
        builder.HasIndex(t => t.TenantId)
            .HasDatabaseName("IX_Tasks_TenantId");

        builder.HasIndex(t => new { t.TenantId, t.ProjectId })
            .HasDatabaseName("IX_Tasks_TenantId_ProjectId");

        builder.HasIndex(t => new { t.ProjectId, t.Status, t.DueDate })
            .HasDatabaseName("IX_Tasks_ProjectId_Status_DueDate");

        builder.HasIndex(t => t.IsDeleted)
            .HasDatabaseName("IX_Tasks_IsDeleted");
    }
}
