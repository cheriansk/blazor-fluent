// [NEED-HUMAN-REVIEW]
using BlazorEnterprise.Core.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorEnterprise.Persistence.Configurations.Tenancy;

/// <summary>
/// EF Core Fluent API configuration for <see cref="ProgramTeamEntity"/> (<c>TG010</c>).
/// </summary>
public sealed class ProgramTeamEntityConfiguration : IEntityTypeConfiguration<ProgramTeamEntity>
{
    public void Configure(EntityTypeBuilder<ProgramTeamEntity> builder)
    {
        builder.ToTable("ProgramTeams", "app");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        builder.Property(x => x.OwnerNames)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(x => x.DeletedBy)
            .HasMaxLength(256);

        builder.HasIndex(x => new { x.TenantId, x.ProgramId, x.Name })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");
    }
}
