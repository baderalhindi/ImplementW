using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Infrastructure.Persistence.Configurations.MasterDataConfig;

internal sealed class RiskRatingDefinitionConfiguration : IEntityTypeConfiguration<RiskRatingDefinition>
{
    public void Configure(EntityTypeBuilder<RiskRatingDefinition> builder)
    {
        builder.ToTable("risk_rating_definition", "master_data_config");
        builder.Property(e => e.Code).HasMaxLength(50);
        builder.HasIndex(e => new { e.ConfigurationVersionId, e.Code }).IsUnique();
        builder.HasBilingualLabel(e => e.Label, "label");

        builder.HasOne<ConfigurationVersion>().WithMany().HasForeignKey(e => e.ConfigurationVersionId).OnDelete(DeleteBehavior.Cascade);
    }
}
