using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.MasterDataConfig;
using PMPlatform.Domain.Reports;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Reports;

internal sealed class SavedViewColumnConfiguration : IEntityTypeConfiguration<SavedViewColumn>
{
    public void Configure(EntityTypeBuilder<SavedViewColumn> builder)
    {
        builder.ToTable("saved_view_column", "reports");
        builder.HasIndex(e => new { e.SavedViewId, e.ReportAllowlistEntryId }).IsUnique();
        builder.HasIndex(e => new { e.SavedViewId, e.SortOrder }).IsUnique();
        builder.HasCheck("sort_order", "sort_order >= 1");

        builder.HasOne<SavedView>().WithMany().HasForeignKey(e => e.SavedViewId).OnDelete(DeleteBehavior.Cascade);

        // The allowlist's enforcement in the database (ERD §5.20): a composition column can name an allowlist entry and nothing else.
        builder.HasOne<ReportAllowlistEntry>().WithMany().HasForeignKey(e => e.ReportAllowlistEntryId).OnDelete(DeleteBehavior.Restrict);
    }
}
