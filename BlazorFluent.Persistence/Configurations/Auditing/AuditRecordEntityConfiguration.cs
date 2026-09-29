using BlazorFluent.Core.Domain.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorFluent.Persistence.Configurations.Auditing;

public class AuditRecordEntityConfiguration : IEntityTypeConfiguration<AuditRecordEntity>
{
    public void Configure(EntityTypeBuilder<AuditRecordEntity> builder)
    {
        builder.ToTable("AuditRecords", EntitySchemas.app.ToString());

        builder.HasKey(a => a.Id);

        builder.Property(a => a.TenantId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(a => a.UserId)
            .HasMaxLength(256)
            .IsRequired(false);

        builder.Property(a => a.UserEmail)
            .HasMaxLength(256)
            .IsRequired(false);

        builder.Property(a => a.EntityName)
            .HasMaxLength(200)
            .IsRequired(false);

        builder.Property(a => a.EntityId)
            .HasMaxLength(128)
            .IsRequired(false);

        builder.Property(a => a.Description)
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(a => a.IpAddress)
            .HasMaxLength(64)
            .IsRequired(false);

        builder.Property(a => a.TraceId)
            .HasMaxLength(128)
            .IsRequired(false);

        // Store JSON diffs in PostgreSQL JSONB
        builder.Property(a => a.ChangesJson)
            .HasColumnType("jsonb")
            .IsRequired(false);

        // Indexes for high performance querying
        builder.HasIndex(a => a.TenantId)
            .HasDatabaseName("IX_AuditRecords_TenantId");

        builder.HasIndex(a => new { a.TenantId, a.Created })
            .HasDatabaseName("IX_AuditRecords_TenantId_Created");

        builder.HasIndex(a => a.UserId)
            .HasDatabaseName("IX_AuditRecords_UserId");

        builder.HasIndex(a => a.EventType)
            .HasDatabaseName("IX_AuditRecords_EventType");
    }
}
