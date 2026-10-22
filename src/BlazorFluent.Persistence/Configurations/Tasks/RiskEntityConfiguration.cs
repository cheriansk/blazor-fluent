using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Core.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorFluent.Persistence.Configurations.Tasks;

public class RiskEntityConfiguration : IEntityTypeConfiguration<RiskEntity>
{
    public void Configure(EntityTypeBuilder<RiskEntity> builder)
    {
        builder.ToTable("Risks", EntitySchemas.tasks.ToString());

        builder.HasKey(r => r.Id);

        builder.Property(r => r.TenantId)
            .IsRequired();

        builder.Property(r => r.Level)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(TaskLevelEnum.Project);

        builder.Property(r => r.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(r => r.Description)
            .HasMaxLength(10000)
            .IsRequired(false);

        builder.Property(r => r.Impact)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(r => r.Probability)
            .IsRequired()
            .HasConversion<int>();

        builder.Ignore(r => r.ExposureScore);

        builder.Property(r => r.MitigationStrategy)
            .HasMaxLength(5000)
            .IsRequired(false)
            .HasDefaultValue(string.Empty);

        builder.Property(r => r.ContingencyPlan)
            .HasMaxLength(5000)
            .IsRequired(false)
            .HasDefaultValue(string.Empty);

        builder.Property(r => r.RiskStatus)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(r => r.Priority)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(r => r.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(r => r.StartDate)
            .IsRequired();

        builder.Property(r => r.DueDate)
            .IsRequired();

        builder.Property(r => r.RevisedDueDate)
            .IsRequired(false);

        builder.Property(r => r.EndDate)
            .IsRequired(false);

        builder.Property(r => r.ActualStartDate)
            .IsRequired(false);

        builder.Property(r => r.ActualEndDate)
            .IsRequired(false);

        builder.Property(r => r.ReporterEmail)
            .HasMaxLength(256)
            .IsRequired()
            .HasDefaultValue(string.Empty);

        builder.Property(r => r.AssigneeEmails)
            .HasMaxLength(2000)
            .IsRequired(false)
            .HasDefaultValue(string.Empty);

        builder.Property(r => r.WatcherEmails)
            .HasMaxLength(2000)
            .IsRequired(false)
            .HasDefaultValue(string.Empty);

        builder.Property(r => r.Labels)
            .HasMaxLength(500)
            .IsRequired(false)
            .HasDefaultValue(string.Empty);

        builder.Property(r => r.ProgramId)
            .IsRequired(false);

        builder.Property(r => r.ProjectId)
            .IsRequired(false);

        builder.Property(r => r.MilestoneId)
            .IsRequired(false);

        builder.Property(r => r.UserId)
            .HasMaxLength(256)
            .IsRequired(false);

        // Soft delete properties
        builder.Property(r => r.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(r => r.DeletedAtUtc)
            .IsRequired(false);

        builder.Property(r => r.DeletedBy)
            .HasMaxLength(256)
            .IsRequired(false);

        // Targeted High-Performance Compound Indexes
        builder.HasIndex(r => r.TenantId)
            .HasDatabaseName("IX_Risks_TenantId");

        builder.HasIndex(r => new { r.TenantId, r.Level })
            .HasDatabaseName("IX_Risks_TenantId_Level");

        builder.HasIndex(r => new { r.TenantId, r.ProgramId })
            .HasDatabaseName("IX_Risks_TenantId_ProgramId");

        builder.HasIndex(r => new { r.TenantId, r.ProjectId })
            .HasDatabaseName("IX_Risks_TenantId_ProjectId");

        builder.HasIndex(r => new { r.TenantId, r.Level, r.RiskStatus, r.DueDate })
            .HasDatabaseName("IX_Risks_Tenant_Level_RiskStatus_DueDate");
    }
}
