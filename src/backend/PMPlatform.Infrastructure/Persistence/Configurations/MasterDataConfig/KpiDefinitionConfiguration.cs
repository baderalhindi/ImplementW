using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Infrastructure.Persistence.Configurations.MasterDataConfig;

internal sealed class KpiDefinitionConfiguration : IEntityTypeConfiguration<KpiDefinition>
{
    public void Configure(EntityTypeBuilder<KpiDefinition> builder)
    {
        builder.ToTable("kpi_definition", "master_data_config");
        builder.Property(e => e.Code).HasMaxLength(50);
        builder.HasIndex(e => e.Code).IsUnique();
        builder.HasBilingualLabel(e => e.Name, "name");
        builder.HasBilingualLabel(e => e.Description, "description");
        builder.HasGovernedLifecycle();

        builder.HasOne<MasterDataItem>().WithMany().HasForeignKey(e => e.UnitItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
