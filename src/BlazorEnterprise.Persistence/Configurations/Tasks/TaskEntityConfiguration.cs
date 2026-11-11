using BlazorEnterprise.Shared.DataListTypes;
using BlazorEnterprise.Core.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorEnterprise.Persistence.Configurations.Tasks;

public partial class TaskEntityConfiguration : IEntityTypeConfiguration<TaskEntity>
{
    public void Configure(EntityTypeBuilder<TaskEntity> builder)
    {
        // EF Core Table-per-Concrete-Type (TPC) inheritance mapping
        builder.UseTpcMappingStrategy();

        builder.HasKey(t => t.Id);

        builder.Property(t => t.TenantId)
            .HasColumnOrder(0)
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
            .IsRequired(false);

        builder.Property(t => t.EstimatedHours)
            .HasPrecision(18, 2)
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

        // Ignore [NotMapped] edge relations
        builder.Property(t => t.ProgramId).IsRequired(false);
        builder.Property(t => t.ProjectId).IsRequired(false);
        builder.Property(t => t.MilestoneId).IsRequired(false);

        // Relationships
        builder.HasMany(t => t.SubTasks)
            .WithOne(s => s.ParentTask)
            .HasForeignKey(s => s.ParentTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        // Hook for downstream entity mapping customizations
        OnConfigureCustom(builder);
    }

    partial void OnConfigureCustom(EntityTypeBuilder<TaskEntity> builder);
}
