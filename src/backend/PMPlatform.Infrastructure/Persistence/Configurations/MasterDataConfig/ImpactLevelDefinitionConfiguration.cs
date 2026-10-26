using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Infrastructure.Persistence.Configurations.MasterDataConfig;

internal sealed class ImpactLevelDefinitionConfiguration : IEntityTypeConfiguration<ImpactLevelDefinition>
{
    public void Configure(EntityTypeBuilder<ImpactLevelDefinition> builder)
    {
        builder.ToTable("impact_level_definition", "master_data_config");
        builder.HasIndex(e => new { e.ConfigurationVersionId, e.ImpactDimensionItemId, e.Level }).IsUnique();
        builder.HasBilingualLabel(e => e.Label, "label");
        builder.HasBilingualLabel(e => e.Description, "description");
        builder.Property(e => e.LowerBound).HasPrecision(18, 4);
        builder.Property(e => e.UpperBound).HasPrecision(18, 4);

        // ADR-011: every impact dimension is scored on five levels.
        builder.HasCheck("level", "level BETWEEN 1 AND 5");

        builder.HasOne<ConfigurationVersion>().WithMany().HasForeignKey(e => e.ConfigurationVersionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MasterDataItem>().WithMany().HasForeignKey(e => e.ImpactDimensionItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
