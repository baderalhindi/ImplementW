using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Infrastructure.Persistence.Configurations.MasterDataConfig;

internal sealed class MasterDataCatalogueConfiguration : IEntityTypeConfiguration<MasterDataCatalogue>
{
    public void Configure(EntityTypeBuilder<MasterDataCatalogue> builder)
    {
        builder.ToTable("master_data_catalogue", "master_data_config");
        builder.Property(e => e.Code).HasMaxLength(50);
        builder.HasIndex(e => e.Code).IsUnique();
        builder.HasBilingualLabel(e => e.Name, "name");
        builder.Property(e => e.AllowsHierarchy).HasDatabaseDefault(false);
        builder.Property(e => e.IsSystem).HasDatabaseDefault(false);
    }
}
