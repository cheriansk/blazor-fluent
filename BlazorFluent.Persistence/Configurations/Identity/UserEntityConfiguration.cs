using BlazorFluent.Core.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorFluent.Persistence.Configurations.Identity;

public class UserEntityConfiguration : IEntityTypeConfiguration<UserEntity>
{
    public void Configure(EntityTypeBuilder<UserEntity> builder)
    {
        builder.ToTable("Users", "identity");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(u => u.FullName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(u => u.UserType)
            .IsRequired();

        builder.Property(u => u.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(u => u.DefaultTenantId)
            .HasMaxLength(256)
            .IsRequired(false);

        builder.Property(u => u.StartDateUtc)
            .IsRequired();

        builder.Property(u => u.EndDateUtc)
            .IsRequired();

        builder.Property(u => u.RowSignature)
            .HasMaxLength(500)
            .IsRequired(false);

        // EF Core 10 ComplexType Value Object mapping for physical address
        builder.ComplexProperty(u => u.PhysicalAddress, addrBuilder =>
        {
            addrBuilder.Property(a => a.Street).HasMaxLength(256);
            addrBuilder.Property(a => a.City).HasMaxLength(100);
            addrBuilder.Property(a => a.State).HasMaxLength(100);
            addrBuilder.Property(a => a.PostalCode).HasMaxLength(20);
            addrBuilder.Property(a => a.Country).HasMaxLength(100);
        });

        // Soft delete
        builder.Property(u => u.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(u => u.DeletedBy).HasMaxLength(256).IsRequired(false);

        // Indexes
        builder.HasIndex(u => u.Email)
            .HasDatabaseName("IX_Users_Email")
            .HasFilter("\"IsDeleted\" = false")
            .IsUnique();

        builder.HasIndex(u => u.DefaultTenantId)
            .HasDatabaseName("IX_Users_DefaultTenantId");
    }
}
