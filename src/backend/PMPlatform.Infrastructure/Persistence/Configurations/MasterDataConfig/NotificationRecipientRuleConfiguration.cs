using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Infrastructure.Persistence.Configurations.MasterDataConfig;

internal sealed class NotificationRecipientRuleConfiguration : IEntityTypeConfiguration<NotificationRecipientRule>
{
    public void Configure(EntityTypeBuilder<NotificationRecipientRule> builder)
    {
        builder.ToTable("notification_recipient_rule", "master_data_config");
        builder.HasIndex(e => new { e.NotificationEventFamilyId, e.RoleId }).IsUnique();

        builder.HasOne<NotificationEventFamily>().WithMany().HasForeignKey(e => e.NotificationEventFamilyId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Role>().WithMany().HasForeignKey(e => e.RoleId).OnDelete(DeleteBehavior.Restrict);
    }
}
