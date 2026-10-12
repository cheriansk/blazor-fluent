using BlazorFluent.Core.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorFluent.Persistence.Configurations.Identity;

public class ImpersonationGrantEntityConfiguration : IEntityTypeConfiguration<ImpersonationGrantEntity>
{
    public void Configure(EntityTypeBuilder<ImpersonationGrantEntity> builder)
    {
        builder.ToTable("ImpersonationGrants", EntitySchemas.identity.ToString());

        builder.HasKey(g => g.Id);

        builder.Property(g => g.SourceAdminId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(g => g.SourceAdminEmail)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(g => g.TargetUserId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(g => g.TargetUserEmail)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(g => g.TargetTenantId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(g => g.StartedAtUtc)
            .IsRequired();

        builder.Property(g => g.ExpiresAtUtc)
            .IsRequired();

        builder.Property(g => g.Reason)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(g => g.IsRevoked)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(g => g.RevokedAtUtc)
            .IsRequired(false);

        // Soft delete
        builder.Property(g => g.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(g => g.DeletedBy).HasMaxLength(256).IsRequired(false);
        builder.Property(g => g.DeletedAtUtc).IsRequired(false);

        // Indexes
        builder.HasIndex(g => new { g.SourceAdminId, g.IsRevoked })
            .HasDatabaseName("IX_ImpersonationGrants_Admin_Revoked")
            .HasFilter("\"IsDeleted\" = false");

        builder.HasIndex(g => g.ExpiresAtUtc)
            .HasDatabaseName("IX_ImpersonationGrants_ExpiresAtUtc");
    }
}
