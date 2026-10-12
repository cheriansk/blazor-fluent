using BlazorFluent.Core.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorFluent.Persistence.Configurations.Tenancy;

public class ProjectMilestoneEntityConfiguration : IEntityTypeConfiguration<ProjectMilestoneEntity>
{
    public void Configure(EntityTypeBuilder<ProjectMilestoneEntity> builder)
    {
        builder.ToTable("ProjectMilestones", EntitySchemas.app.ToString());

        builder.HasKey(m => m.Id);

        builder.Property(m => m.TenantId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(m => m.ProjectId)
            .IsRequired();

        builder.Property(m => m.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(m => m.Description)
            .HasMaxLength(4000)
            .IsRequired(false);

        builder.Property(m => m.StartDateUtc)
            .IsRequired();

        builder.Property(m => m.EndDateUtc)
            .IsRequired();

        builder.Property(m => m.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(m => m.OrderIndex)
            .IsRequired()
            .HasDefaultValue(0);

        // Shadow relationship to ProjectEntity
        builder.HasOne<ProjectEntity>()
            .WithMany()
            .HasForeignKey(m => m.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(m => m.TenantId)
            .HasDatabaseName("IX_ProjectMilestones_TenantId");

        builder.HasIndex(m => new { m.TenantId, m.ProjectId })
            .HasDatabaseName("IX_ProjectMilestones_TenantId_ProjectId");

        builder.HasIndex(m => new { m.ProjectId, m.Status, m.EndDateUtc })
            .HasDatabaseName("IX_ProjectMilestones_Project_Status_EndDate");
    }
}
