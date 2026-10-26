using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Infrastructure.Persistence.Configurations.MasterDataConfig;

internal sealed class GovernanceProfileSettingConfiguration : IEntityTypeConfiguration<GovernanceProfileSetting>
{
    public void Configure(EntityTypeBuilder<GovernanceProfileSetting> builder)
    {
        builder.ToTable("governance_profile_setting", "master_data_config");
        builder.HasIndex(e => new { e.ConfigurationVersionId, e.GovernanceProfileItemId }).IsUnique();

        builder.HasOne<ConfigurationVersion>().WithMany().HasForeignKey(e => e.ConfigurationVersionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MasterDataItem>().WithMany().HasForeignKey(e => e.GovernanceProfileItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MasterDataItem>().WithMany().HasForeignKey(e => e.DocumentControlLevelItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
