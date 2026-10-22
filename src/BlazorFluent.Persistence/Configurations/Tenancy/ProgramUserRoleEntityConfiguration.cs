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

        builder.Property(r => r.ProgramId)
            .IsRequired();

        builder.Property(r => r.UserEntityId)
            .IsRequired();

        builder.Property(r => r.Role)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(r => r.RowSignature)
            .HasMaxLength(128)
            .IsRequired(false);

        builder.HasIndex(r => r.TenantId);
        builder.HasIndex(r => new { r.TenantId, r.ProgramId, r.UserEntityId }).IsUnique().HasFilter("\"IsDeleted\" = false");
        builder.HasIndex(r => new { r.ProgramId, r.Role });

        builder.HasOne(r => r.Program)
            .WithMany(p => p.UserRoles)
            .HasForeignKey(r => r.ProgramId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.User)
            .WithMany(u => u.ProgramRoles)
            .HasForeignKey(r => r.UserEntityId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
