using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Infrastructure.Persistence.Configurations.MasterDataConfig;

internal sealed class ConfigurationFamilyConfiguration : IEntityTypeConfiguration<ConfigurationFamily>
{
    public void Configure(EntityTypeBuilder<ConfigurationFamily> builder)
    {
        builder.ToTable("configuration_family", "master_data_config");
        builder.Property(e => e.Code).HasMaxLength(50);
        builder.HasIndex(e => e.Code).IsUnique();
        builder.HasBilingualLabel(e => e.Name, "name");
    }
}
