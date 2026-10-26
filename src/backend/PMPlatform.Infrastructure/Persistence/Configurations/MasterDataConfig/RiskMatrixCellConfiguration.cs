using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Infrastructure.Persistence.Configurations.MasterDataConfig;

internal sealed class RiskMatrixCellConfiguration : IEntityTypeConfiguration<RiskMatrixCell>
{
    public void Configure(EntityTypeBuilder<RiskMatrixCell> builder)
    {
        builder.ToTable("risk_matrix_cell", "master_data_config");
        builder.HasIndex(e => new { e.ConfigurationVersionId, e.ProbabilityLevel, e.ImpactLevel }).IsUnique();

        // ADR-011: a 5×5 matrix.
        builder.HasCheck("probability_level", "probability_level BETWEEN 1 AND 5");
        builder.HasCheck("impact_level", "impact_level BETWEEN 1 AND 5");

        builder.HasOne<ConfigurationVersion>().WithMany().HasForeignKey(e => e.ConfigurationVersionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<RiskRatingDefinition>().WithMany().HasForeignKey(e => e.RiskRatingDefinitionId).OnDelete(DeleteBehavior.Restrict);
    }
}
