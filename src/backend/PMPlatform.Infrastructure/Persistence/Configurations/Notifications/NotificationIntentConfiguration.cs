using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.Notifications;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Notifications;

internal sealed class NotificationIntentConfiguration : IEntityTypeConfiguration<NotificationIntent>
{
    /// <summary>Named: the generated name exceeds PostgreSQL's 63 characters. EV-4: one intent per source occurrence.</summary>
    public const string SourceKey = "ix_notification_intent_source_event_type_source_reference";

    public void Configure(EntityTypeBuilder<NotificationIntent> builder)
    {
        builder.ToTable("notification_intent", "notifications");
        builder.Property(e => e.SourceModule).HasMaxLength(50);
        builder.Property(e => e.SourceEventType).HasMaxLength(100);
        builder.Property(e => e.SourceReference).HasMaxLength(200);
        builder.Property(e => e.EventFamilyCode).HasMaxLength(100);
        builder.Property(e => e.SubjectType).HasMaxLength(100);
        builder.Property(e => e.SuppressionReason).HasMaxLength(100);
        builder.Property(e => e.DeepLink).HasMaxLength(500);
        builder.HasCheck("reason", "(status IN ('SUPPRESSED', 'FAILED')) = (suppression_reason IS NOT NULL)");
        builder.HasCheck("scheduled", "status <> 'SCHEDULED' OR scheduled_for IS NOT NULL");

        builder.HasIndex(e => new { e.SourceEventType, e.SourceReference }).IsUnique().HasDatabaseName(SourceKey);

        // The worker's queue — intents to route, least recently tried first — and its completion sweep: worker queries,
        // indexed by their owner (indexing-strategy.md §1).
        builder.HasIndex(e => new { e.UpdatedAt, e.Id }).HasFilter("status IN ('RECEIVED', 'SCHEDULED')").HasDatabaseName("ix_notification_intent_route_queue");
        builder.HasIndex(e => e.Id).HasFilter("status = 'ROUTED'").HasDatabaseName("ix_notification_intent_routed");

        // The operations list, newest first.
        builder.HasIndex(e => new { e.ReceivedAt, e.Id });

        builder.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ScopeProjectId).OnDelete(DeleteBehavior.Restrict);

        builder.HasRowVersion();
    }
}
