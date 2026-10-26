using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.ChangeRequest;
using PMPlatform.Domain.FinancialKpi;
using PMPlatform.Domain.MasterDataConfig;
using PMPlatform.Domain.Schedule;
using ChangeRequestEntity = PMPlatform.Domain.ChangeRequest.ChangeRequest;

namespace PMPlatform.Infrastructure.Persistence.Configurations.ChangeRequest;

internal sealed class MaterialityEvaluationConfiguration : IEntityTypeConfiguration<MaterialityEvaluation>
{
    public void Configure(EntityTypeBuilder<MaterialityEvaluation> builder)
    {
        builder.ToTable("materiality_evaluation", "change_request");
        builder.IsAppendOnly();

        // The review records one authoritative evaluation per revision; a request's latest is read with it.
        builder.HasIndex(e => new { e.ChangeRequestId, e.RevisionNo }).IsUnique();

        builder.HasCheck("revision_no", "revision_no >= 1");
        builder.HasCheck(
            "bands",
            "resulting_band_no BETWEEN 1 AND 3 AND (cost_band_no IS NULL OR cost_band_no BETWEEN 1 AND resulting_band_no) "
            + "AND (schedule_band_no IS NULL OR schedule_band_no BETWEEN 1 AND resulting_band_no) "
            + "AND (scope_band_no IS NULL OR scope_band_no BETWEEN 1 AND resulting_band_no)");

        // A pinned commitment comes with its version and the figure a percentage threshold is of; a dimension evaluated has its commitment.
        builder.HasCheck(
            "baseline_pin",
            "(project_baseline_id IS NULL) = (project_baseline_version_no IS NULL) AND (project_baseline_id IS NULL) = (baseline_duration_days IS NULL) "
            + "AND (schedule_band_no IS NULL OR project_baseline_id IS NOT NULL) AND (baseline_duration_days IS NULL OR baseline_duration_days >= 1)");
        builder.HasCheck(
            "budget_pin",
            "(financial_commitment_id IS NULL) = (financial_commitment_version_no IS NULL) AND (financial_commitment_id IS NULL) = (baseline_budget_sar IS NULL) "
            + "AND (cost_band_no IS NULL) = (financial_commitment_id IS NULL)");

        builder.HasOne<ChangeRequestEntity>().WithMany().HasForeignKey(e => e.ChangeRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ConfigurationVersion>().WithMany().HasForeignKey(e => e.MaterialityConfigurationVersionId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_materiality_evaluation_materiality_configuration_version");
        builder.HasOne<ProjectBaseline>().WithMany().HasForeignKey(e => e.ProjectBaselineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FinancialCommitment>().WithMany().HasForeignKey(e => e.FinancialCommitmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
