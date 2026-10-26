using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.Notifications;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Notifications;

internal sealed class NotificationIntentParameterConfiguration : IEntityTypeConfiguration<NotificationIntentParameter>
{
    /// <summary>Named: the generated name exceeds PostgreSQL's 63 characters.</summary>
    public const string ParameterKey = "ix_notification_intent_parameter_intent_key";

    public void Configure(EntityTypeBuilder<NotificationIntentParameter> builder)
    {
        builder.ToTable("notification_intent_parameter", "notifications");
        builder.Property(e => e.ParameterKey).HasMaxLength(100);
        builder.IsAppendOnly();

        builder.HasIndex(e => new { e.NotificationIntentId, e.ParameterKey }).IsUnique().HasDatabaseName(ParameterKey);

        builder.HasOne<NotificationIntent>().WithMany().HasForeignKey(e => e.NotificationIntentId).OnDelete(DeleteBehavior.Cascade);
    }
}
