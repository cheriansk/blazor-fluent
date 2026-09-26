using BlazorFluent.Core.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorFluent.Persistence.Configurations.Tenancy;

public class ProjectEntityConfiguration : IEntityTypeConfiguration<ProjectEntity>
{
    public void Configure(EntityTypeBuilder<ProjectEntity> builder)
    {
        builder.ToTable("Projects", "tenancy");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.TenantId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(p => p.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.Description)
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(p => p.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // Soft delete
        builder.Property(p => p.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(p => p.DeletedBy).HasMaxLength(256).IsRequired(false);


        builder.HasIndex(p => p.TenantId)
            .HasDatabaseName("IX_Projects_TenantId");

        builder.HasIndex(p => new { p.TenantId, p.Name })
            .HasDatabaseName("IX_Projects_TenantId_Name");
    }
}
