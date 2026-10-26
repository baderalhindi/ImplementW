using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Domain.MasterDataConfig;
using PMPlatform.Domain.Risk;
using RiskEntity = PMPlatform.Domain.Risk.Risk;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Risk;

internal sealed class RiskAssessmentVersionConfiguration : IEntityTypeConfiguration<RiskAssessmentVersion>
{
    public void Configure(EntityTypeBuilder<RiskAssessmentVersion> builder)
    {
        builder.ToTable("risk_assessment_version", "risk");
        builder.IsAppendOnly();
        builder.HasNarrative(e => e.Rationale, "rationale");

        // ERD: one version number per risk; SCR-081 reads a risk's latest assessment by it (indexing-strategy.md F-3).
        builder.HasIndex(e => new { e.RiskId, e.VersionNo }).IsUnique();

        builder.HasCheck("version_no", "version_no >= 1");
        builder.HasCheck("probability_level", "probability_level BETWEEN 1 AND 5");
        builder.HasCheck("overall_impact_level", "overall_impact_level BETWEEN 1 AND 5");

        builder.HasOne<RiskEntity>().WithMany().HasForeignKey(e => e.RiskId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.AssessedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ConfigurationVersion>().WithMany().HasForeignKey(e => e.MatrixConfigurationVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RiskRatingDefinition>().WithMany().HasForeignKey(e => e.RiskRatingDefinitionId).OnDelete(DeleteBehavior.Restrict);
    }
}
