using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.FinancialKpi;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Infrastructure.Persistence.Configurations.FinancialKpi;

internal sealed class FinancialCommitmentLineConfiguration : IEntityTypeConfiguration<FinancialCommitmentLine>
{
    public void Configure(EntityTypeBuilder<FinancialCommitmentLine> builder)
    {
        builder.ToTable("financial_commitment_line", "financial_kpi");
        builder.HasIndex(e => new { e.FinancialCommitmentId, e.EtimadCategoryItemId }).IsUnique();
        builder.HasCheck("amount", "amount_sar >= 0");

        builder.HasOne<FinancialCommitment>().WithMany().HasForeignKey(e => e.FinancialCommitmentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MasterDataItem>().WithMany().HasForeignKey(e => e.EtimadCategoryItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
