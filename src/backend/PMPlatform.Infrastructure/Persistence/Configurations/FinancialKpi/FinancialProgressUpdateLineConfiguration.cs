using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.FinancialKpi;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Infrastructure.Persistence.Configurations.FinancialKpi;

internal sealed class FinancialProgressUpdateLineConfiguration : IEntityTypeConfiguration<FinancialProgressUpdateLine>
{
    public void Configure(EntityTypeBuilder<FinancialProgressUpdateLine> builder)
    {
        builder.ToTable("financial_progress_update_line", "financial_kpi");
        builder.HasIndex(e => new { e.FinancialProgressUpdateId, e.EtimadCategoryItemId }).IsUnique();
        builder.HasCheck("amount", "actual_sar >= 0");

        builder.HasOne<FinancialProgressUpdate>().WithMany().HasForeignKey(e => e.FinancialProgressUpdateId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MasterDataItem>().WithMany().HasForeignKey(e => e.EtimadCategoryItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
