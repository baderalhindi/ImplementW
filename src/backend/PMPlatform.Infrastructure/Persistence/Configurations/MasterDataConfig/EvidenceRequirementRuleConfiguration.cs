using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Infrastructure.Persistence.Configurations.MasterDataConfig;

internal sealed class EvidenceRequirementRuleConfiguration : IEntityTypeConfiguration<EvidenceRequirementRule>
{
    public void Configure(EntityTypeBuilder<EvidenceRequirementRule> builder)
    {
        builder.ToTable("evidence_requirement_rule", "master_data_config");
        builder.HasIndex(e => new { e.ConfigurationVersionId, e.MilestoneCategoryItemId, e.EvidenceTypeItemId }).IsUnique();

        builder.HasOne<ConfigurationVersion>().WithMany().HasForeignKey(e => e.ConfigurationVersionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MasterDataItem>().WithMany().HasForeignKey(e => e.MilestoneCategoryItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MasterDataItem>().WithMany().HasForeignKey(e => e.EvidenceTypeItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
