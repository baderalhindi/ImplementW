using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.FinancialKpi;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Domain.Progress;
using PMPlatform.Domain.Project;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Infrastructure.Persistence.Configurations.FinancialKpi;

internal sealed class FinancialProgressUpdateConfiguration : IEntityTypeConfiguration<FinancialProgressUpdate>
{
    /// <summary>At most one revision of a period on its way, DRAFT, SUBMITTED or UNDER_REVIEW, for any writer (financial-kpi.md D-7).</summary>
    public const string SingleOpenKey = "ix_financial_progress_update_open_reporting_cycle_id";

    public void Configure(EntityTypeBuilder<FinancialProgressUpdate> builder)
    {
        builder.ToTable("financial_progress_update", "financial_kpi");
        builder.HasRowVersion();
        builder.Property(e => e.RevisionNo).HasDatabaseDefault(1);
        builder.Property(e => e.SourceReference).HasMaxLength(200);
        builder.HasNarrative(e => e.Narrative, "narrative");
        builder.HasNarrative(e => e.ReturnReason, "return_reason");
        builder.HasIndex(e => new { e.ReportingCycleId, e.RevisionNo }).IsUnique();
        builder.HasIndex(e => e.ReportingCycleId, SingleOpenKey).IsUnique()
            .HasFilter("status IN ('DRAFT', 'SUBMITTED', 'UNDER_REVIEW')").HasDatabaseName(SingleOpenKey);

        // TASK-026 (indexing-strategy.md I-39): SCR-071 Financial Progress and the SCR-049 Financials tab.
        builder.HasIndex(e => new { e.ProjectId, e.AsOfDate });

        builder.HasCheck("revision_no", "revision_no >= 1");
        builder.HasCheck("amounts", "(actual_expenditure_to_date_sar IS NULL OR actual_expenditure_to_date_sar >= 0) AND (forecast_at_completion_sar IS NULL OR forecast_at_completion_sar >= 0)");

        // TASK-052: a figure is present exactly when MEASURED — never a 0 standing in for Unknown — and a forecast only beside it.
        builder.HasCheck("value_status", "(value_status = 'MEASURED') = (actual_expenditure_to_date_sar IS NOT NULL)");
        builder.HasCheck("forecast", "forecast_at_completion_sar IS NULL OR actual_expenditure_to_date_sar IS NOT NULL");
        builder.HasCheck("source_reference", "source_type = 'MANUAL' OR source_reference IS NOT NULL");

        // Who submitted and who decided are set by the transitions that make them so, and only then.
        builder.HasCheck("submitted", "(status = 'DRAFT') = (submitted_at IS NULL) AND (submitted_at IS NULL) = (submitted_by_user_id IS NULL)");
        builder.HasCheck("reviewed", "(status IN ('RETURNED', 'PUBLISHED')) = (reviewed_at IS NOT NULL) AND (reviewed_at IS NULL) = (reviewed_by_user_id IS NULL)");
        builder.HasCheck("return_reason", "(status = 'RETURNED') = (return_reason IS NOT NULL)");

        builder.HasOne<ReportingCycle>().WithMany().HasForeignKey(e => e.ReportingCycleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectIntake>().WithMany().HasForeignKey(e => e.ProjectIntakeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.EnteredByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.SubmittedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
