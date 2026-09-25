using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Infrastructure.Persistence.Configurations.MasterDataConfig;

internal sealed class ConfigurationValueConfiguration : IEntityTypeConfiguration<ConfigurationValue>
{
    public void Configure(EntityTypeBuilder<ConfigurationValue> builder)
    {
        builder.ToTable("configuration_value", "master_data_config");
        builder.Property(e => e.ValueKey).HasMaxLength(100);
        builder.Property(e => e.ValueText).HasMaxLength(500);
        builder.HasIndex(e => new { e.ConfigurationVersionId, e.ValueKey }).IsUnique();

        builder.HasOne<ConfigurationVersion>().WithMany().HasForeignKey(e => e.ConfigurationVersionId).OnDelete(DeleteBehavior.Cascade);
    }
}
