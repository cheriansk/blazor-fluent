using BlazorFluent.Core.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorFluent.Persistence.Configurations.Events;

public class EventPublishTrackerEntityConfiguration : IEntityTypeConfiguration<EventPublishTrackerEntity>
{
    public void Configure(EntityTypeBuilder<EventPublishTrackerEntity> builder)
    {
        builder.ToTable("EventPublishTrackers", EntitySchemas.app.ToString());

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.CorrelationId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(e => e.EventName)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.EventTypeFullName)
            .HasMaxLength(512)
            .IsRequired(false);

        builder.Property(e => e.SourceClass)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.SourceMethod)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.SourceFilePath)
            .HasMaxLength(512)
            .IsRequired(false);

        builder.Property(e => e.UserId)
            .HasMaxLength(256)
            .IsRequired(false);

        builder.Property(e => e.UserEmail)
            .HasMaxLength(256)
            .IsRequired(false);

        builder.Property(e => e.TriggerSource)
            .HasMaxLength(128)
            .IsRequired();

        // High performance PostgreSQL JSONB column for the payload
        builder.Property(e => e.PayloadJson)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.PublishedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        // 1-to-many relationship with consumptions
        builder.HasMany(e => e.Consumptions)
            .WithOne(c => c.PublishTracker)
            .HasForeignKey(c => c.EventPublishTrackerId)
            .OnDelete(DeleteBehavior.Cascade);

        // Fast query indexes
        builder.HasIndex(e => e.TenantId)
            .HasDatabaseName("IX_EventPublishTrackers_TenantId");

        builder.HasIndex(e => e.EventId)
            .HasDatabaseName("IX_EventPublishTrackers_EventId");

        builder.HasIndex(e => e.CorrelationId)
            .HasDatabaseName("IX_EventPublishTrackers_CorrelationId");

        builder.HasIndex(e => e.EventName)
            .HasDatabaseName("IX_EventPublishTrackers_EventName");

        builder.HasIndex(e => e.Status)
            .HasDatabaseName("IX_EventPublishTrackers_Status");

        builder.HasIndex(e => new { e.TenantId, e.PublishedAtUtc })
            .HasDatabaseName("IX_EventPublishTrackers_TenantId_PublishedAtUtc");
    }
}
