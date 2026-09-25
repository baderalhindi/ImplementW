using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Infrastructure.Persistence.Configurations.MasterDataConfig;

internal sealed class ParticipationContributionRuleConfiguration : IEntityTypeConfiguration<ParticipationContributionRule>
{
    public void Configure(EntityTypeBuilder<ParticipationContributionRule> builder)
    {
        builder.ToTable("participation_contribution_rule", "master_data_config");
        builder.HasIndex(e => new { e.ConfigurationVersionId, e.ParticipationMode, e.ContributionTypeItemId }).IsUnique();

        builder.HasOne<ConfigurationVersion>().WithMany().HasForeignKey(e => e.ConfigurationVersionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MasterDataItem>().WithMany().HasForeignKey(e => e.ContributionTypeItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
