using BlazorFluent.Core.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorFluent.Persistence.Configurations.Tenancy;

public class TenantEntityConfiguration : IEntityTypeConfiguration<TenantEntity>
{
    public void Configure(EntityTypeBuilder<TenantEntity> builder)
    {
        builder.ToTable("Tenants", EntitySchemas.tenancy.ToString());

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Slug)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(t => t.Slug)
            .IsUnique()
            .HasDatabaseName("IX_Tenants_Slug");

        builder.Property(t => t.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.InternalEmailDomains)
            .HasMaxLength(500)
            .IsRequired()
            .HasDefaultValue("");

        builder.Property(t => t.ExternalEmailDomains)
            .HasMaxLength(500)
            .IsRequired()
            .HasDefaultValue("");

        builder.Property(t => t.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(t => t.StartDate)
            .IsRequired();

        builder.Property(t => t.EndDate)
            .IsRequired();

        // One tenant → many projects
        builder.HasMany(t => t.Projects)
            .WithOne(p => p.TenantEntity)
            .HasForeignKey(p => p.TenantEntityId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
