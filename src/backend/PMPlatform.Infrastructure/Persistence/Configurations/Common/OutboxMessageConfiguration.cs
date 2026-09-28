using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.Common;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Common;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_message", "common");
        builder.Property(e => e.SourceModule).HasMaxLength(50);
        builder.Property(e => e.MessageType).HasMaxLength(100);
        builder.Property(e => e.MessageKey).HasMaxLength(200);
        builder.Property(e => e.Payload).HasColumnType("jsonb");
        builder.Property(e => e.AttemptCount).HasDatabaseDefault(0);

        // event-conventions EV-4: a message cannot be published twice.
        builder.HasIndex(e => new { e.MessageType, e.MessageKey }).IsUnique();

        // The dispatcher's queue: undispatched messages, oldest first (a worker query, indexed by its owner).
        builder.HasIndex(e => new { e.OccurredAt, e.Id }).HasFilter("dispatched_at IS NULL");
    }
}
