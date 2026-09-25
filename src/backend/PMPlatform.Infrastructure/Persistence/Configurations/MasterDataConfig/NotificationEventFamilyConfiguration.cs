using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Infrastructure.Persistence.Configurations.MasterDataConfig;

internal sealed class NotificationEventFamilyConfiguration : IEntityTypeConfiguration<NotificationEventFamily>
{
    public void Configure(EntityTypeBuilder<NotificationEventFamily> builder)
    {
        builder.ToTable("notification_event_family", "master_data_config");
        builder.Property(e => e.Code).HasMaxLength(100);
        builder.HasIndex(e => new { e.ConfigurationVersionId, e.Code }).IsUnique();
        builder.HasBilingualLabel(e => e.Label, "label");

        builder.HasOne<ConfigurationVersion>().WithMany().HasForeignKey(e => e.ConfigurationVersionId).OnDelete(DeleteBehavior.Cascade);
    }
}
