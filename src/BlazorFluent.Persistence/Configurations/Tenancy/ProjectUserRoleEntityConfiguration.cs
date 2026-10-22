using BlazorFluent.Core.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorFluent.Persistence.Configurations.Tenancy;

public class ProjectUserRoleEntityConfiguration : IEntityTypeConfiguration<ProjectUserRoleEntity>
{
    public void Configure(EntityTypeBuilder<ProjectUserRoleEntity> builder)
    {
        builder.ToTable("ProjectUserRoles", EntitySchemas.tenancy.ToString());

        builder.HasKey(r => r.Id);

        builder.Property(r => r.TenantId)
            .IsRequired();

        builder.Property(r => r.ProjectId)
            .IsRequired();

        builder.Property(r => r.UserEntityId)
            .IsRequired();

        builder.Property(r => r.Role)
            .IsRequired();

        builder.Property(r => r.RowSignature)
            .HasMaxLength(128)
            .IsRequired(false);

        // Soft delete
        builder.Property(r => r.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(r => r.DeletedBy).HasMaxLength(256).IsRequired(false);

        // Foreign keys & navigation
        builder.HasOne(r => r.Project)
            .WithMany(p => p.UserRoles)
            .HasForeignKey(r => r.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.User)
            .WithMany(u => u.ProjectRoles)
            .HasForeignKey(r => r.UserEntityId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(r => r.TenantId)
            .HasDatabaseName("IX_ProjectUserRoles_TenantId");

        builder.HasIndex(r => new { r.TenantId, r.ProjectId, r.UserEntityId })
            .IsUnique()
            .HasDatabaseName("IX_ProjectUserRoles_Tenant_Project_User")
            .HasFilter("\"IsDeleted\" = false");

        builder.HasIndex(r => new { r.ProjectId, r.Role })
            .HasDatabaseName("IX_ProjectUserRoles_Project_Role");
    }
}
