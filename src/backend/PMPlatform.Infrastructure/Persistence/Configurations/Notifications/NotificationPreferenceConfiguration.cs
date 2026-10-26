using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Domain.Notifications;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Notifications;

internal sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    /// <summary>Named: the generated name exceeds PostgreSQL's 63 characters.</summary>
    public const string ChoiceKey = "ix_notification_preference_user_family_channel";

    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("notification_preference", "notifications");
        builder.Property(e => e.EventFamilyCode).HasMaxLength(100);

        // ADR-004: in-app is always on, so only e-mail and SMS are chosen.
        builder.HasCheck("channel_choice", "channel IN ('EMAIL', 'SMS')");

        builder.HasIndex(e => new { e.UserId, e.EventFamilyCode, e.Channel }).IsUnique().HasDatabaseName(ChoiceKey);

        builder.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasRowVersion();
    }
}
