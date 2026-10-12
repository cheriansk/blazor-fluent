using BlazorFluent.Core.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorFluent.Persistence.Configurations.Tenancy;

public class ProgramEntityConfiguration : IEntityTypeConfiguration<ProgramEntity>
{
    public void Configure(EntityTypeBuilder<ProgramEntity> builder)
    {
        builder.ToTable("programs", "tenancy");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.TenantId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(p => p.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.Description)
            .HasMaxLength(2000);

        builder.Property(p => p.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(p => p.IsActive)
            .IsRequired();

        builder.Property(p => p.IsDeleted)
            .IsRequired();

        builder.HasIndex(p => p.TenantId);
        builder.HasIndex(p => new { p.TenantId, p.Code }).IsUnique().HasFilter("\"IsDeleted\" = false");
        builder.HasIndex(p => new { p.TenantId, p.Name });
        builder.HasIndex(p => p.Status);

        builder.HasOne(p => p.TenantEntity)
            .WithMany(t => t.Programs)
            .HasForeignKey(p => p.TenantEntityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Projects)
            .WithOne(pr => pr.Program)
            .HasForeignKey(pr => pr.ProgramId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
