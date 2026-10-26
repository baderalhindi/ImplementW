using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Infrastructure.Persistence.Configurations.MasterDataConfig;

internal sealed class MaterialityBandConfiguration : IEntityTypeConfiguration<MaterialityBand>
{
    public void Configure(EntityTypeBuilder<MaterialityBand> builder)
    {
        builder.ToTable("materiality_band", "master_data_config");
        builder.HasIndex(e => new { e.ConfigurationVersionId, e.GovernanceProfileItemId, e.BandNo }).IsUnique();
        builder.Property(e => e.CostThresholdPct).HasPrecision(18, 4);
        builder.Property(e => e.ScheduleThresholdPct).HasPrecision(18, 4);
        builder.Property(e => e.ScopeRuleCode).HasMaxLength(100);

        // ADR-016: three bands.
        builder.HasCheck("band_no", "band_no BETWEEN 1 AND 3");

        builder.HasOne<ConfigurationVersion>().WithMany().HasForeignKey(e => e.ConfigurationVersionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MasterDataItem>().WithMany().HasForeignKey(e => e.GovernanceProfileItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
