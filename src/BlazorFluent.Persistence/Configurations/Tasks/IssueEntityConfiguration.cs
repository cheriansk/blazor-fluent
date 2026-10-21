using BlazorFluent.Shared.DataListTypes;
using BlazorFluent.Core.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorFluent.Persistence.Configurations.Tasks;

public class IssueEntityConfiguration : IEntityTypeConfiguration<IssueEntity>
{
    public void Configure(EntityTypeBuilder<IssueEntity> builder)
    {
        builder.ToTable("Issues", EntitySchemas.tasks.ToString());

        builder.HasKey(i => i.Id);

        builder.Property(i => i.TenantId)
            .IsRequired();

        builder.Property(i => i.Level)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(TaskLevelEnum.Project);

        builder.Property(i => i.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(i => i.Description)
            .HasMaxLength(10000)
            .IsRequired(false);

        builder.Ignore(i => i.Severity);

        builder.Property(i => i.MilestoneId)
            .IsRequired(false);

        builder.Property(i => i.RootCause)
            .HasMaxLength(5000)
            .IsRequired(false)
            .HasDefaultValue(string.Empty);

        builder.Property(i => i.Workaround)
            .HasMaxLength(5000)
            .IsRequired(false)
            .HasDefaultValue(string.Empty);

        builder.Property(i => i.ResolutionStrategy)
            .HasMaxLength(5000)
            .IsRequired(false)
            .HasDefaultValue(string.Empty);

        builder.Property(i => i.Priority)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(i => i.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(i => i.StartDate)
            .IsRequired();

        builder.Property(i => i.DueDate)
            .IsRequired();

        builder.Property(i => i.RevisedDueDate)
            .IsRequired(false);

        builder.Property(i => i.EndDate)
            .IsRequired(false);

        builder.Property(i => i.ActualStartDate)
            .IsRequired(false);

        builder.Property(i => i.ActualEndDate)
            .IsRequired(false);

        builder.Property(i => i.ReporterEmail)
            .HasMaxLength(256)
            .IsRequired()
            .HasDefaultValue(string.Empty);

        builder.Property(i => i.AssigneeEmails)
            .HasMaxLength(2000)
            .IsRequired(false)
            .HasDefaultValue(string.Empty);

        builder.Property(i => i.WatcherEmails)
            .HasMaxLength(2000)
            .IsRequired(false)
            .HasDefaultValue(string.Empty);

        builder.Property(i => i.Labels)
            .HasMaxLength(500)
            .IsRequired(false)
            .HasDefaultValue(string.Empty);

        builder.Property(i => i.ProgramId)
            .IsRequired(false);

        builder.Property(i => i.ProjectId)
            .IsRequired(false);

        builder.Property(i => i.UserId)
            .HasMaxLength(256)
            .IsRequired(false);

        // Soft delete properties
        builder.Property(i => i.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(i => i.DeletedAtUtc)
            .IsRequired(false);

        builder.Property(i => i.DeletedBy)
            .HasMaxLength(256)
            .IsRequired(false);

        // Targeted High-Performance Compound Indexes
        builder.HasIndex(i => i.TenantId)
            .HasDatabaseName("IX_Issues_TenantId");

        builder.HasIndex(i => new { i.TenantId, i.Level })
            .HasDatabaseName("IX_Issues_TenantId_Level");

        builder.HasIndex(i => new { i.TenantId, i.ProgramId })
            .HasDatabaseName("IX_Issues_TenantId_ProgramId");

        builder.HasIndex(i => new { i.TenantId, i.ProjectId })
            .HasDatabaseName("IX_Issues_TenantId_ProjectId");

        builder.HasIndex(i => new { i.TenantId, i.Level, i.Priority, i.Status })
            .HasDatabaseName("IX_Issues_Tenant_Level_Priority_Status");
    }
}
