using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Infrastructure.Persistence.Configurations.MasterDataConfig;

internal sealed class NotificationChannelRuleConfiguration : IEntityTypeConfiguration<NotificationChannelRule>
{
    public void Configure(EntityTypeBuilder<NotificationChannelRule> builder)
    {
        builder.ToTable("notification_channel_rule", "master_data_config");
        builder.HasIndex(e => new { e.NotificationEventFamilyId, e.Channel }).IsUnique();

        builder.HasOne<NotificationEventFamily>().WithMany().HasForeignKey(e => e.NotificationEventFamilyId).OnDelete(DeleteBehavior.Cascade);
    }
}
