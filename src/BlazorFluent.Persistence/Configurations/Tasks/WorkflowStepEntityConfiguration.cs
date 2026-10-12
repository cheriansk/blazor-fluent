using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Tasks;
using BlazorFluent.Core.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorFluent.Persistence.Configurations.Tasks;

public class WorkflowStepEntityConfiguration : IEntityTypeConfiguration<WorkflowStepEntity>
{
    public void Configure(EntityTypeBuilder<WorkflowStepEntity> builder)
    {
        builder.ToTable("WorkflowSteps", EntitySchemas.tasks.ToString());

        builder.HasKey(s => s.Id);

        builder.Property(s => s.TenantId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(s => s.Level)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(TaskLevel.Project);

        builder.Property(s => s.WorkflowTaskId)
            .IsRequired();

        builder.Property(s => s.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(s => s.Description)
            .HasMaxLength(10000)
            .IsRequired(false);

        builder.Property(s => s.StepOrder)
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(s => s.StepStatus)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(WorkflowStepStatus.Pending);

        builder.Property(s => s.StepOwnerEmail)
            .HasMaxLength(256)
            .IsRequired()
            .HasDefaultValue(string.Empty);

        builder.Property(s => s.Priority)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(TaskPriority.Medium);

        builder.Property(s => s.Status)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(TaskStatus.Open);

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

        builder.Property(s => s.ActivatedAtUtc)
            .IsRequired(false);

        builder.Property(s => s.CompletedAtUtc)
            .IsRequired(false);

        builder.Property(s => s.CompletionNotes)
            .HasMaxLength(4000)
            .IsRequired(false);

        builder.Property(s => s.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(s => s.DeletedAtUtc)
            .IsRequired(false);

        builder.Property(s => s.DeletedBy)
            .HasMaxLength(256)
            .IsRequired(false);

        // Indices
        builder.HasIndex(s => new { s.WorkflowTaskId, s.StepOrder });
        builder.HasIndex(s => new { s.TenantId, s.StepOwnerEmail, s.StepStatus });
    }
}
