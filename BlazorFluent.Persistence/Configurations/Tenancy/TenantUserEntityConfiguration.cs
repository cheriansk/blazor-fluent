using BlazorFluent.Core.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorFluent.Persistence.Configurations.Tenancy;

public class TenantUserEntityConfiguration : IEntityTypeConfiguration<TenantUserEntity>
{
    public void Configure(EntityTypeBuilder<TenantUserEntity> builder)
    {
        builder.ToTable("tenant_users", "tenancy");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.TenantId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.UserId)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(u => u.UserEmail)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(u => u.UserName)
            .HasMaxLength(200);

        builder.Property(u => u.Role)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(u => u.IsActive)
            .IsRequired();

        builder.HasIndex(u => u.TenantId);
        builder.HasIndex(u => new { u.TenantId, u.UserId }).IsUnique().HasFilter("\"IsDeleted\" = false");
        builder.HasIndex(u => new { u.TenantId, u.Role });

        builder.HasOne(u => u.Tenant)
            .WithMany(t => t.TenantUsers)
            .HasForeignKey(u => u.TenantEntityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(u => u.User)
            .WithMany(usr => usr.TenantMemberships)
            .HasForeignKey(u => u.UserEntityId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
