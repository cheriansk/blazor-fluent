using BlazorEnterprise.Core.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorEnterprise.Persistence.Configurations.Tasks;

public class WorkflowTaskEntityConfiguration : IEntityTypeConfiguration<WorkflowTaskEntity>
{
    public void Configure(EntityTypeBuilder<WorkflowTaskEntity> builder)
    {
        builder.ToTable("WorkflowTasks", EntitySchemas.tasks.ToString(), t =>
        {
            t.HasCheckConstraint(
                "CK_WorkflowTasks_WorkflowStepOrder_Positive",
                "\"WorkflowStepOrder\" > 0");
        });

        builder.Property(w => w.WorkflowId)
            .IsRequired();

        builder.Property(w => w.WorkflowStepOrder)
            .IsRequired();

        builder.HasOne(w => w.Workflow)
            .WithMany(wf => wf.Steps)
            .HasForeignKey(w => w.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);

        // Targeted High-Performance Compound Indexes

        builder.HasIndex(w => new { w.TenantId, w.Level })
            .HasDatabaseName("IX_WorkflowTasks_TenantId_Level");

        builder.HasIndex(w => new { w.TenantId, w.WorkflowId, w.WorkflowStepOrder })
            .HasDatabaseName("IX_WorkflowTasks_TenantId_WorkflowId_StepOrder");

        builder.HasIndex(w => new { w.TenantId, w.Level, w.Status, w.DueDate })
            .HasDatabaseName("IX_WorkflowTasks_Tenant_Level_Status_DueDate");
    }
}
