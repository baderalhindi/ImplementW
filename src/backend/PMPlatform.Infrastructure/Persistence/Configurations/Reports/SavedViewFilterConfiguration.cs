using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.MasterDataConfig;
using PMPlatform.Domain.Reports;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Reports;

internal sealed class SavedViewFilterConfiguration : IEntityTypeConfiguration<SavedViewFilter>
{
    public void Configure(EntityTypeBuilder<SavedViewFilter> builder)
    {
        builder.ToTable("saved_view_filter", "reports");
        builder.Property(e => e.ValueText).HasMaxLength(500);

        builder.HasOne<SavedView>().WithMany().HasForeignKey(e => e.SavedViewId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ReportAllowlistEntry>().WithMany().HasForeignKey(e => e.ReportAllowlistEntryId).OnDelete(DeleteBehavior.Restrict);
    }
}
