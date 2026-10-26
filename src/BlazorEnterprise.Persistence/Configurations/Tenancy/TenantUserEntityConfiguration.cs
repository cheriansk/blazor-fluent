using BlazorEnterprise.Core.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorEnterprise.Persistence.Configurations.Tenancy;

public class TenantUserEntityConfiguration : IEntityTypeConfiguration<TenantUserEntity>
{
    public void Configure(EntityTypeBuilder<TenantUserEntity> builder)
    {
        builder.ToTable("tenant_users", "tenancy");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.TenantId)
            .IsRequired();

        builder.Property(u => u.UserEntityId)
            .IsRequired();

        builder.Property(u => u.Role)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(u => u.IsActive)
            .IsRequired();

        builder.Property(u => u.RowSignature)
            .HasMaxLength(128)
            .IsRequired(false);

        builder.HasIndex(u => u.TenantId);
        builder.HasIndex(u => new { u.TenantId, u.UserEntityId }).IsUnique().HasFilter("\"IsDeleted\" = false");
        builder.HasIndex(u => new { u.TenantId, u.Role });

        builder.HasOne(u => u.Tenant)
            .WithMany(t => t.TenantUsers)
            .HasForeignKey(u => u.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(u => u.User)
            .WithMany(usr => usr.TenantMemberships)
            .HasForeignKey(u => u.UserEntityId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
