using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.Notifications;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Notifications;

internal sealed class NotificationTemplateConfiguration : IEntityTypeConfiguration<NotificationTemplate>
{
    /// <summary>Named: the generated name exceeds PostgreSQL's 63 characters. Two versions of one event type and channel given the same number: the second loses.</summary>
    public const string VersionKey = "ix_notification_template_event_type_channel_version_no";

    public void Configure(EntityTypeBuilder<NotificationTemplate> builder)
    {
        builder.ToTable("notification_template", "notifications");
        builder.Property(e => e.EventFamilyCode).HasMaxLength(100);
        builder.Property(e => e.EventType).HasMaxLength(100);
        builder.Property(e => e.SubjectAr).HasMaxLength(500);
        builder.Property(e => e.SubjectEn).HasMaxLength(500);
        builder.HasCheck("version_no", "version_no >= 1");

        // ADR-012: both languages always; ADR-004: an SMS has no subject, e-mail and in-app have one in both.
        builder.HasCheck("subject", "(channel = 'SMS') = (subject_ar IS NULL) AND (subject_ar IS NULL) = (subject_en IS NULL)");

        builder.HasIndex(e => new { e.EventType, e.Channel, e.VersionNo }).IsUnique().HasDatabaseName(VersionKey);

        // Routing's lookup: the PUBLISHED version of an event type on each channel. Not unique: publishing retires the version it
        // replaces in the same save, and a unique index would refuse that save whenever the new row happened to be written first.
        builder.HasIndex(e => new { e.EventType, e.Channel }).HasFilter("lifecycle_state = 'PUBLISHED'").HasDatabaseName("ix_notification_template_published");

        builder.HasGovernedLifecycle();
        builder.HasRowVersion();
    }
}
