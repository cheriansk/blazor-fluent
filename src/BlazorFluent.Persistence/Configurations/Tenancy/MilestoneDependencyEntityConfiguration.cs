using BlazorFluent.Core.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorFluent.Persistence.Configurations.Tenancy;

public class MilestoneDependencyEntityConfiguration : IEntityTypeConfiguration<MilestoneDependencyEntity>
{
    public void Configure(EntityTypeBuilder<MilestoneDependencyEntity> builder)
    {
        builder.ToTable("MilestoneDependencies", EntitySchemas.app.ToString());

        builder.HasKey(d => d.Id);

        builder.Property(d => d.TenantId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(d => d.ProjectId)
            .IsRequired();

        builder.Property(d => d.MilestoneId)
            .IsRequired();

        builder.Property(d => d.DependsOnMilestoneId)
            .IsRequired();

        builder.Property(d => d.Notes)
            .HasMaxLength(1000)
            .IsRequired(false);

        // Relationships to ProjectMilestoneEntity
        builder.HasOne<ProjectMilestoneEntity>()
            .WithMany()
            .HasForeignKey(d => d.MilestoneId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ProjectMilestoneEntity>()
            .WithMany()
            .HasForeignKey(d => d.DependsOnMilestoneId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unique index: prevent duplicate dependency pairs
        builder.HasIndex(d => new { d.MilestoneId, d.DependsOnMilestoneId })
            .IsUnique()
            .HasDatabaseName("IX_MilestoneDependencies_Milestone_DependsOn");

        builder.HasIndex(d => new { d.TenantId, d.ProjectId })
            .HasDatabaseName("IX_MilestoneDependencies_Tenant_Project");
    }
}
