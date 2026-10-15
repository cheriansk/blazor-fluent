using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Core.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorFluent.Persistence.Configurations.Tasks;

public class BlockerDependencyEntityConfiguration : IEntityTypeConfiguration<BlockerDependencyEntity>
{
    public void Configure(EntityTypeBuilder<BlockerDependencyEntity> builder)
    {
        builder.ToTable("BlockerDependencies", EntitySchemas.tasks.ToString());

        builder.HasKey(b => b.Id);

        builder.Property(b => b.TenantId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(b => b.Level)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(TaskLevel.Project);

        builder.Property(b => b.SourceTaskId)
            .IsRequired();

        builder.Property(b => b.TargetTaskId)
            .IsRequired();

        builder.Property(b => b.ImpactDetails)
            .HasMaxLength(5000)
            .IsRequired();

        builder.Property(b => b.Criticality)
            .IsRequired()
            .HasConversion<int>();

        builder.Ignore(b => b.ResolveDueDate);

        builder.Property(b => b.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(b => b.Description)
            .HasMaxLength(10000)
            .IsRequired(false);

        builder.Property(b => b.Priority)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(b => b.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(b => b.StartDate)
            .IsRequired();

        builder.Property(b => b.DueDate)
            .IsRequired();

        builder.Property(b => b.EndDate)
            .IsRequired(false);

        builder.Property(b => b.ReporterEmail)
            .HasMaxLength(256)
            .IsRequired()
            .HasDefaultValue(string.Empty);

        builder.Property(b => b.AssigneeEmails)
            .HasMaxLength(2000)
            .IsRequired(false)
            .HasDefaultValue(string.Empty);

        builder.Property(b => b.WatcherEmails)
            .HasMaxLength(2000)
            .IsRequired(false)
            .HasDefaultValue(string.Empty);

        builder.Property(b => b.Labels)
            .HasMaxLength(500)
            .IsRequired(false)
            .HasDefaultValue(string.Empty);

        builder.Property(b => b.ProgramId)
            .IsRequired(false);

        builder.Property(b => b.ProjectId)
            .IsRequired(false);

        builder.Property(b => b.UserId)
            .HasMaxLength(256)
            .IsRequired(false);

        // Soft delete properties
        builder.Property(b => b.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(b => b.DeletedAtUtc)
            .IsRequired(false);

        builder.Property(b => b.DeletedBy)
            .HasMaxLength(256)
            .IsRequired(false);

        // Relationships to Source and Target Tasks
        builder.HasOne(b => b.SourceTask)
            .WithMany()
            .HasForeignKey(b => b.SourceTaskId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.TargetTask)
            .WithMany()
            .HasForeignKey(b => b.TargetTaskId)
            .OnDelete(DeleteBehavior.Restrict);

        // Targeted High-Performance Compound Indexes
        builder.HasIndex(b => b.TenantId)
            .HasDatabaseName("IX_BlockerDependencies_TenantId");

        builder.HasIndex(b => b.SourceTaskId)
            .HasDatabaseName("IX_BlockerDependencies_SourceTaskId");

        builder.HasIndex(b => b.TargetTaskId)
            .HasDatabaseName("IX_BlockerDependencies_TargetTaskId");

        builder.HasIndex(b => new { b.TenantId, b.TargetTaskId, b.Status, b.DueDate })
            .HasDatabaseName("IX_BlockerDependencies_Tenant_Target_Status_DueDate");
    }
}
