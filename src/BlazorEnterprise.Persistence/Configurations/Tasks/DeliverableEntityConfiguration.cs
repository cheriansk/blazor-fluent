using BlazorEnterprise.Core.Domain.Tasks;
using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorEnterprise.Persistence.Configurations.Tasks;

public class DeliverableEntityConfiguration : IEntityTypeConfiguration<DeliverableEntity>
{
    public void Configure(EntityTypeBuilder<DeliverableEntity> builder)
    {
        builder.ToTable("Deliverables", EntitySchemas.tasks.ToString());

        builder.HasKey(d => d.Id);

        builder.Property(d => d.TenantId)
            .HasColumnOrder(0)
            .IsRequired();

        builder.Property(d => d.Level)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(TaskLevelEnum.Project);

        builder.Property(d => d.ParentType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(d => d.ParentItemId)
            .IsRequired();

        builder.Property(d => d.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(d => d.Description)
            .HasMaxLength(10000)
            .IsRequired(false);

        builder.Property(d => d.SignOffStatus)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(DeliverableStatusEnum.Draft);

        builder.Property(d => d.ArtifactUrl)
            .HasMaxLength(2000)
            .IsRequired(false);

        builder.Property(d => d.SignOffNotes)
            .HasMaxLength(5000)
            .IsRequired(false);

        builder.Property(d => d.ApprovedBy)
            .HasMaxLength(256)
            .IsRequired(false);

        builder.Property(d => d.ApprovedAtUtc)
            .IsRequired(false);

        builder.Property(d => d.Priority)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(d => d.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(d => d.StartDate)
            .IsRequired();

        builder.Property(d => d.DueDate)
            .IsRequired();

        builder.Property(d => d.RevisedDueDate)
            .IsRequired(false);

        builder.Property(d => d.EndDate)
            .IsRequired(false);

        builder.Property(d => d.ActualStartDate)
            .IsRequired(false);

        builder.Property(d => d.ActualEndDate)
            .IsRequired(false);

        builder.Property(d => d.ReporterEmail)
            .HasMaxLength(256)
            .IsRequired()
            .HasDefaultValue(string.Empty);

        builder.Property(d => d.AssigneeEmails)
            .HasMaxLength(2000)
            .IsRequired(false)
            .HasDefaultValue(string.Empty);

        builder.Property(d => d.WatcherEmails)
            .HasMaxLength(2000)
            .IsRequired(false)
            .HasDefaultValue(string.Empty);

        builder.Property(d => d.Labels)
            .HasMaxLength(500)
            .IsRequired(false)
            .HasDefaultValue(string.Empty);

        // Ignore [NotMapped] edge relations
        builder.Property(d => d.ProgramId).IsRequired(false);
        builder.Property(d => d.ProjectId).IsRequired(false);
        builder.Ignore(d => d.MilestoneId);

        // Soft delete properties
        builder.Property(d => d.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(d => d.DeletedAtUtc)
            .IsRequired(false);

        builder.Property(d => d.DeletedBy)
            .HasMaxLength(256)
            .IsRequired(false);

        // Targeted High-Performance Compound Indexes
        builder.HasIndex(d => d.TenantId)
            .HasDatabaseName("IX_Deliverables_TenantId");

        builder.HasIndex(d => new { d.TenantId, d.Level })
            .HasDatabaseName("IX_Deliverables_TenantId_Level");

        builder.HasIndex(d => new { d.TenantId, d.Level, d.Status, d.DueDate })
            .HasDatabaseName("IX_Deliverables_Tenant_Level_Status_DueDate");

        builder.HasIndex(d => new { d.TenantId, d.ParentType, d.ParentItemId })
            .HasDatabaseName("IX_Deliverables_Tenant_ParentType_ParentItemId");

        builder.HasIndex(d => new { d.TenantId, d.SignOffStatus })
            .HasDatabaseName("IX_Deliverables_Tenant_SignOffStatus");
    }
}
