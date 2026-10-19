using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Core.Domain.Tasks;
using BlazorFluent.Core.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorFluent.Persistence.Configurations.Tasks;

public class WorkflowEntityConfiguration : IEntityTypeConfiguration<WorkflowEntity>
{
    public void Configure(EntityTypeBuilder<WorkflowEntity> builder)
    {
        builder.ToTable("Workflows", EntitySchemas.tasks.ToString());

        builder.HasKey(w => w.Id);

        builder.Property(w => w.TenantId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(w => w.Level)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(TaskLevel.Project);

        builder.Property(w => w.Title)
            .HasMaxLength(200)
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

        builder.Property(w => w.EndDate)
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

        builder.Property(w => w.WorkflowOwnerEmail)
            .HasMaxLength(256)
            .IsRequired()
            .HasDefaultValue(string.Empty);

        builder.Property(w => w.CurrentStepId)
            .IsRequired(false);

        builder.Property(w => w.ProgramId)
            .IsRequired(false);

        builder.Property(w => w.ProjectId)
            .IsRequired(false);

        builder.Property(w => w.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(w => w.DeletedAtUtc)
            .IsRequired(false);

        builder.Property(w => w.DeletedBy)
            .HasMaxLength(256)
            .IsRequired(false);

        // Targeted Multi-Level Compound Indices
        builder.HasIndex(w => new { w.TenantId, w.Level });
        builder.HasIndex(w => new { w.TenantId, w.ProgramId });
        builder.HasIndex(w => new { w.TenantId, w.ProjectId, w.Status });
        builder.HasIndex(w => new { w.TenantId, w.WorkflowOwnerEmail });

        // Navigation to Tasks as Steps
        builder.HasMany(w => w.Steps)
            .WithOne(t => t.Workflow)
            .HasForeignKey(t => t.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
