using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Infrastructure.Persistence.Configurations.MasterDataConfig;

internal sealed class KpiPolicyRuleConfiguration : IEntityTypeConfiguration<KpiPolicyRule>
{
    public void Configure(EntityTypeBuilder<KpiPolicyRule> builder)
    {
        builder.ToTable("kpi_policy_rule", "master_data_config");
        builder.HasIndex(e => new { e.ConfigurationVersionId, e.KpiDefinitionId }).IsUnique();
        builder.Property(e => e.GreenThreshold).HasPrecision(18, 4);
        builder.Property(e => e.AmberThreshold).HasPrecision(18, 4);

        builder.HasOne<ConfigurationVersion>().WithMany().HasForeignKey(e => e.ConfigurationVersionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<KpiDefinition>().WithMany().HasForeignKey(e => e.KpiDefinitionId).OnDelete(DeleteBehavior.Restrict);
    }
}
