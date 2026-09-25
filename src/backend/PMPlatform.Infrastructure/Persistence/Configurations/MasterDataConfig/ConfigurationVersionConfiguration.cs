using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Infrastructure.Persistence.Configurations.MasterDataConfig;

internal sealed class ConfigurationVersionConfiguration : IEntityTypeConfiguration<ConfigurationVersion>
{
    public void Configure(EntityTypeBuilder<ConfigurationVersion> builder)
    {
        builder.ToTable("configuration_version", "master_data_config");
        builder.HasIndex(e => new { e.ConfigurationFamilyId, e.VersionNo }).IsUnique();
        builder.HasNarrative(e => e.ChangeSummary, "change_summary");
        builder.HasGovernedLifecycle();

        builder.HasOne<ConfigurationFamily>().WithMany().HasForeignKey(e => e.ConfigurationFamilyId).OnDelete(DeleteBehavior.Restrict);
    }
}
