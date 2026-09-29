using BlazorFluent.Core.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorFluent.Persistence.Configurations.Identity;

public class UserSessionEntityConfiguration : IEntityTypeConfiguration<UserSessionEntity>
{
    public void Configure(EntityTypeBuilder<UserSessionEntity> builder)
    {
        builder.ToTable("UserSessions", EntitySchemas.identity.ToString());

        builder.HasKey(s => s.Id);

        builder.Property(s => s.TenantId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(s => s.UserId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(s => s.UserEmail)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(s => s.IpAddress)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.UserAgent)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(s => s.StartedAtUtc)
            .IsRequired();

        builder.Property(s => s.LastActivityAtUtc)
            .IsRequired();

        builder.Property(s => s.IsRevoked)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(s => s.RevokedAtUtc)
            .IsRequired(false);

        builder.Property(s => s.RevokedBy)
            .HasMaxLength(256)
            .IsRequired(false);

        // Soft delete
        builder.Property(s => s.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(s => s.DeletedBy).HasMaxLength(256).IsRequired(false);
        builder.Property(s => s.DeletedAtUtc).IsRequired(false);

        // Indexes
        builder.HasIndex(s => s.TenantId)
            .HasDatabaseName("IX_UserSessions_TenantId");

        builder.HasIndex(s => new { s.TenantId, s.UserId, s.IsRevoked })
            .HasDatabaseName("IX_UserSessions_Tenant_User_Revoked")
            .HasFilter("\"IsDeleted\" = false");

        builder.HasIndex(s => s.StartedAtUtc)
            .HasDatabaseName("IX_UserSessions_StartedAtUtc");
    }
}
