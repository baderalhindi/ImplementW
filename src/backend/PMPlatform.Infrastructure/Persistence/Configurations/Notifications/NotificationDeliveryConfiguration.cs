using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Domain.Notifications;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Notifications;

internal sealed class NotificationDeliveryConfiguration : IEntityTypeConfiguration<NotificationDelivery>
{
    /// <summary>Named: the generated name exceeds PostgreSQL's 63 characters. One delivery per intent, recipient and channel.</summary>
    public const string RecipientKey = "ix_notification_delivery_intent_recipient_channel";

    public void Configure(EntityTypeBuilder<NotificationDelivery> builder)
    {
        builder.ToTable("notification_delivery", "notifications");
        builder.Property(e => e.RenderedSubject).HasMaxLength(500);
        builder.Property(e => e.SuppressionReason).HasMaxLength(100);
        builder.Property(e => e.ProviderMessageId).HasMaxLength(200);
        builder.Property(e => e.AttemptCount).HasDatabaseDefault(0);
        builder.HasCheck("attempt_count", "attempt_count >= 0");
        builder.HasCheck("reason", "(status = 'SUPPRESSED') = (suppression_reason IS NOT NULL)");
        builder.HasCheck("dead_letter", "(status = 'DEAD_LETTER') = (dead_lettered_at IS NOT NULL)");
        builder.HasCheck("read", "(status = 'READ') = (read_at IS NOT NULL) AND (read_at IS NULL OR channel = 'IN_APP')");
        builder.HasCheck("segment_count", "(segment_count IS NULL OR segment_count >= 1) AND (segment_count IS NULL OR channel = 'SMS')");

        builder.HasIndex(e => new { e.NotificationIntentId, e.RecipientUserId, e.Channel }).IsUnique().HasDatabaseName(RecipientKey);

        // indexing-strategy.md I-46: SCR-150–153, the caller's notifications newest first. I-47: the unread badge (TASK-040).
        builder.HasIndex(e => new { e.RecipientUserId, e.Channel, e.CreatedAt, e.Id });
        builder.HasIndex(e => new { e.RecipientUserId, e.Channel, e.ReadAt });

        // The worker's queue: e-mail and SMS to send or retry, earliest due first (a worker query, indexed by its owner).
        builder.HasIndex(e => new { e.NextAttemptAt, e.Id }).HasFilter("status IN ('PENDING', 'FAILED')").HasDatabaseName("ix_notification_delivery_send_queue");

        builder.HasOne<NotificationIntent>().WithMany().HasForeignKey(e => e.NotificationIntentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<NotificationTemplate>().WithMany().HasForeignKey(e => e.NotificationTemplateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RecipientUserId).OnDelete(DeleteBehavior.Restrict);

        // Two workers deciding one delivery at once cannot both commit; a read and a retry neither.
        builder.HasRowVersion();
    }
}
