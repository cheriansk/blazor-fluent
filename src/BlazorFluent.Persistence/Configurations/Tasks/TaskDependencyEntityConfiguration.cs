using BlazorFluent.Core.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorFluent.Persistence.Configurations.Tasks;

public class TaskDependencyEntityConfiguration : IEntityTypeConfiguration<TaskDependencyEntity>
{
    public void Configure(EntityTypeBuilder<TaskDependencyEntity> builder)
    {
        builder.ToTable("TaskDependencies", EntitySchemas.app.ToString());

        builder.HasKey(d => d.Id);

        builder.Property(d => d.TenantId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(d => d.ProjectId)
            .IsRequired();

        builder.Property(d => d.TaskId)
            .IsRequired();

        builder.Property(d => d.DependsOnTaskId)
            .IsRequired();

        builder.Property(d => d.DependencyType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(d => d.ResolveByUtc)
            .IsRequired(false);

        builder.Property(d => d.Notes)
            .HasMaxLength(1000)
            .IsRequired(false);

        // Shadow relationships to UserTaskEntity
        builder.HasOne<UserTaskEntity>()
            .WithMany()
            .HasForeignKey(d => d.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<UserTaskEntity>()
            .WithMany()
            .HasForeignKey(d => d.DependsOnTaskId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(d => d.DependsOnProjectId)
            .IsRequired(false);

        // Unique index: prevent duplicate dependency pairs
        builder.HasIndex(d => new { d.TaskId, d.DependsOnTaskId })
            .IsUnique()
            .HasDatabaseName("IX_TaskDependencies_Task_DependsOn");

        builder.HasIndex(d => new { d.TenantId, d.ProjectId })
            .HasDatabaseName("IX_TaskDependencies_Tenant_Project");

        builder.HasIndex(d => d.DependsOnProjectId)
            .HasDatabaseName("IX_TaskDependencies_DependsOnProject");

        builder.HasIndex(d => new { d.DependsOnTaskId, d.ResolveByUtc })
            .HasDatabaseName("IX_TaskDependencies_DependsOn_ResolveBy");
    }
}
