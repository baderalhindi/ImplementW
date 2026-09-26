using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Infrastructure.Persistence.Configurations.MasterDataConfig;

internal sealed class MasterDataItemConfiguration : IEntityTypeConfiguration<MasterDataItem>
{
    public void Configure(EntityTypeBuilder<MasterDataItem> builder)
    {
        builder.ToTable("master_data_item", "master_data_config");
        builder.Property(e => e.Code).HasMaxLength(50);
        builder.HasIndex(e => new { e.CatalogueId, e.Code }).IsUnique();
        builder.HasBilingualLabel(e => e.Label, "label");
        builder.HasBilingualLabel(e => e.Description, "description");
        builder.Property(e => e.SortOrder).HasDatabaseDefault(0);
        builder.Property(e => e.IsSystem).HasDatabaseDefault(false);
        builder.HasGovernedLifecycle();

        builder.HasOne<MasterDataCatalogue>().WithMany().HasForeignKey(e => e.CatalogueId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MasterDataItem>().WithMany().HasForeignKey(e => e.ParentItemId).OnDelete(DeleteBehavior.Restrict);

        // TASK-026 (indexing-strategy.md I-09): a catalogue's items in one state, in display order (ADM-020 to ADM-029
        // and every form's controlled-value list).
        builder.HasIndex(e => new { e.CatalogueId, e.LifecycleState, e.SortOrder, e.Id });
    }
}
