using BlazorFluent.Core.Domain.Tasks;
using BlazorFluent.Core.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorFluent.Persistence.Configurations.Tasks;

public class UserTaskEntityConfiguration : IEntityTypeConfiguration<UserTaskEntity>
{
    public void Configure(EntityTypeBuilder<UserTaskEntity> builder)
    {
        builder.ToTable("Tasks", EntitySchemas.app.ToString());

        builder.HasKey(t => t.Id);

        builder.Property(t => t.TenantId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(t => t.ProjectId)
            .IsRequired();

        builder.Property(t => t.MilestoneId)
            .IsRequired(false);

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
            .IsRequired();

        builder.Property(t => t.ReporterEmail)
            .HasMaxLength(256)
            .IsRequired()
            .HasDefaultValue(string.Empty);

        builder.Property(t => t.AssigneeEmails)
            .HasMaxLength(2000)
            .IsRequired(false)
            .HasDefaultValue(string.Empty);

        builder.Property(t => t.Labels)
            .HasMaxLength(500)
            .IsRequired(false)
            .HasDefaultValue(string.Empty);

        // Shadow relationship to ProjectEntity (retains PostgreSQL foreign key constraint without bloating C# entity model)
        builder.HasOne<ProjectEntity>()
            .WithMany()
            .HasForeignKey(t => t.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        // Optional relationship to ProjectMilestoneEntity
        builder.HasOne<ProjectMilestoneEntity>()
            .WithMany()
            .HasForeignKey(t => t.MilestoneId)
            .OnDelete(DeleteBehavior.SetNull);

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

        builder.HasIndex(t => new { t.TenantId, t.ProjectId, t.Status, t.DueDate })
            .HasDatabaseName("IX_Tasks_Tenant_Project_Status_DueDate");

        builder.HasIndex(t => t.MilestoneId)
            .HasDatabaseName("IX_Tasks_MilestoneId");
    }
}
