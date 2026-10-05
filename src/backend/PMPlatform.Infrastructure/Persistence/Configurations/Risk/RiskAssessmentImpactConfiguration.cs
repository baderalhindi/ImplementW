using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.MasterDataConfig;
using PMPlatform.Domain.Risk;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Risk;

internal sealed class RiskAssessmentImpactConfiguration : IEntityTypeConfiguration<RiskAssessmentImpact>
{
    public void Configure(EntityTypeBuilder<RiskAssessmentImpact> builder)
    {
        builder.ToTable("risk_assessment_impact", "risk");
        builder.IsAppendOnly();
        builder.HasNarrative(e => e.Rationale, "rationale");

        // ADR-011: one row per dimension of an assessment.
        builder.HasIndex(e => new { e.RiskAssessmentVersionId, e.ImpactDimensionItemId }).IsUnique();

        builder.HasCheck("impact_level", "impact_level BETWEEN 1 AND 5");

        builder.HasOne<RiskAssessmentVersion>().WithMany().HasForeignKey(e => e.RiskAssessmentVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MasterDataItem>().WithMany().HasForeignKey(e => e.ImpactDimensionItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
