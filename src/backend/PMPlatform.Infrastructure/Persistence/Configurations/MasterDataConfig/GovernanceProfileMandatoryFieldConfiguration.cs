using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Infrastructure.Persistence.Configurations.MasterDataConfig;

internal sealed class GovernanceProfileMandatoryFieldConfiguration : IEntityTypeConfiguration<GovernanceProfileMandatoryField>
{
    public void Configure(EntityTypeBuilder<GovernanceProfileMandatoryField> builder)
    {
        builder.ToTable("governance_profile_mandatory_field", "master_data_config");
        builder.Property(e => e.FieldCode).HasMaxLength(100);
        builder.HasIndex(e => new { e.GovernanceProfileSettingId, e.FieldCode }).IsUnique();

        builder.HasOne<GovernanceProfileSetting>().WithMany().HasForeignKey(e => e.GovernanceProfileSettingId).OnDelete(DeleteBehavior.Cascade);
    }
}
