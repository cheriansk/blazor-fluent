using BlazorFluent.Core.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorFluent.Persistence.Configurations.Tenancy;

public class ProgramUserRoleEntityConfiguration : IEntityTypeConfiguration<ProgramUserRoleEntity>
{
    public void Configure(EntityTypeBuilder<ProgramUserRoleEntity> builder)
    {
        builder.ToTable("program_user_roles", "tenancy");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.TenantId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(r => r.UserId)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(r => r.UserEmail)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(r => r.UserName)
            .HasMaxLength(200);

        builder.Property(r => r.Role)
            .IsRequired()
            .HasConversion<int>();

        builder.HasIndex(r => r.TenantId);
        builder.HasIndex(r => new { r.TenantId, r.ProgramId, r.UserId }).IsUnique().HasFilter("\"IsDeleted\" = false");
        builder.HasIndex(r => new { r.ProgramId, r.Role });

        builder.HasOne(r => r.Program)
            .WithMany(p => p.UserRoles)
            .HasForeignKey(r => r.ProgramId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
