using BlazorFluent.Core.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorFluent.Persistence.Configurations.Events;

public class EventConsumptionTrackerEntityConfiguration : IEntityTypeConfiguration<EventConsumptionTrackerEntity>
{
    public void Configure(EntityTypeBuilder<EventConsumptionTrackerEntity> builder)
    {
        builder.ToTable("EventConsumptionTrackers", EntitySchemas.app.ToString());

        builder.HasKey(c => c.Id);

        builder.Property(c => c.TenantId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(c => c.CorrelationId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(c => c.ConsumerClass)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(c => c.ConsumerMethod)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(c => c.StartedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(c => c.CompletedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        builder.Property(c => c.ErrorMessage)
            .HasMaxLength(2000)
            .IsRequired(false);

        builder.Property(c => c.ExceptionDetails)
            .HasColumnType("text")
            .IsRequired(false);

        // Indexes
        builder.HasIndex(c => c.TenantId)
            .HasDatabaseName("IX_EventConsumptionTrackers_TenantId");

        builder.HasIndex(c => c.EventPublishTrackerId)
            .HasDatabaseName("IX_EventConsumptionTrackers_PublishTrackerId");

        builder.HasIndex(c => c.ConsumerClass)
            .HasDatabaseName("IX_EventConsumptionTrackers_ConsumerClass");

        builder.HasIndex(c => c.Status)
            .HasDatabaseName("IX_EventConsumptionTrackers_Status");
    }
}
