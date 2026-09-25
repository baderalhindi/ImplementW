using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Infrastructure.Persistence.Configurations.MasterDataConfig;

internal sealed class ProbabilityLevelDefinitionConfiguration : IEntityTypeConfiguration<ProbabilityLevelDefinition>
{
    public void Configure(EntityTypeBuilder<ProbabilityLevelDefinition> builder)
    {
        builder.ToTable("probability_level_definition", "master_data_config");
        builder.HasIndex(e => new { e.ConfigurationVersionId, e.Level }).IsUnique();
        builder.HasBilingualLabel(e => e.Label, "label");
        builder.Property(e => e.LowerPct).HasPrecision(18, 4);
        builder.Property(e => e.UpperPct).HasPrecision(18, 4);

        // PTBC-017: the probability axis of the 5×5 matrix.
        builder.HasCheck("level", "level BETWEEN 1 AND 5");

        builder.HasOne<ConfigurationVersion>().WithMany().HasForeignKey(e => e.ConfigurationVersionId).OnDelete(DeleteBehavior.Cascade);
    }
}
