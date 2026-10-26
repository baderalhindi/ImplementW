using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.FinancialKpi;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Domain.MasterDataConfig;
using PMPlatform.Domain.Progress;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Infrastructure.Persistence.Configurations.FinancialKpi;

internal sealed class PublishedFinancialSnapshotConfiguration : IEntityTypeConfiguration<PublishedFinancialSnapshot>
{
    public void Configure(EntityTypeBuilder<PublishedFinancialSnapshot> builder)
    {
        builder.ToTable("published_financial_snapshot", "financial_kpi");
        builder.IsAppendOnly();
        builder.Property(e => e.SourceReference).HasMaxLength(200);
        builder.HasIndex(e => e.FinancialProgressUpdateId).IsUnique();

        // One official record per period (financial-kpi.md D-7).
        builder.HasIndex(e => e.ReportingCycleId).IsUnique();

        // TASK-026 (indexing-strategy.md I-40): FG-01 reads a project's latest published financial position and its trend.
        builder.HasIndex(e => new { e.ProjectId, e.AsOfDate });

        builder.HasCheck("value_status", "(value_status = 'MEASURED') = (actual_expenditure_to_date_sar IS NOT NULL)");
        builder.HasCheck("budget", "(financial_commitment_id IS NULL) = (approved_budget_sar IS NULL)");

        // A rated status needs every figure it is rated from: UNKNOWN is the only answer without them.
        builder.HasCheck(
            "financial_status",
            "financial_status = 'UNKNOWN' OR (value_status = 'MEASURED' AND approved_budget_sar > 0 AND forecast_at_completion_sar IS NOT NULL)");

        builder.HasOne<ReportingCycle>().WithMany().HasForeignKey(e => e.ReportingCycleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FinancialProgressUpdate>().WithMany().HasForeignKey(e => e.FinancialProgressUpdateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FinancialCommitment>().WithMany().HasForeignKey(e => e.FinancialCommitmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.PublishedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.EnteredByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ConfigurationVersion>().WithMany().HasForeignKey(e => e.ThresholdConfigurationVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}
