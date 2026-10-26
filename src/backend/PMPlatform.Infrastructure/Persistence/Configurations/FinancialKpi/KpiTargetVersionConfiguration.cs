using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.FinancialKpi;

namespace PMPlatform.Infrastructure.Persistence.Configurations.FinancialKpi;

internal sealed class KpiTargetVersionConfiguration : IEntityTypeConfiguration<KpiTargetVersion>
{
    /// <summary>One ACTIVE target version per assignment — the one a measurement recorded now pins — for any writer.</summary>
    public const string SingleActiveKey = "ix_kpi_target_version_active_kpi_assignment_id";

    /// <summary>At most one version per assignment on its way (financial-kpi.md D-5).</summary>
    public const string SingleOpenKey = "ix_kpi_target_version_open_kpi_assignment_id";

    public void Configure(EntityTypeBuilder<KpiTargetVersion> builder)
    {
        builder.ToTable("kpi_target_version", "financial_kpi");
        builder.HasRowVersion();
        builder.Property(e => e.RevisionNo).HasDatabaseDefault(1);
        builder.Property(e => e.TargetValue).HasPrecision(18, 4);
        builder.Property(e => e.GreenThreshold).HasPrecision(18, 4);
        builder.Property(e => e.AmberThreshold).HasPrecision(18, 4);
        builder.HasIndex(e => new { e.KpiAssignmentId, e.VersionNo }).IsUnique();
        builder.HasIndex(e => e.KpiAssignmentId, SingleActiveKey).IsUnique().HasFilter("status = 'ACTIVE'").HasDatabaseName(SingleActiveKey);
        builder.HasIndex(e => e.KpiAssignmentId, SingleOpenKey).IsUnique()
            .HasFilter("status IN ('DRAFT', 'SUBMITTED', 'UNDER_REVIEW', 'RETURNED')").HasDatabaseName(SingleOpenKey);

        builder.HasCheck("numbers", "version_no >= 1 AND revision_no >= 1");
        builder.HasCheck("thresholds", "(green_threshold IS NULL) = (amber_threshold IS NULL)");
        builder.HasCheck("activated", "(status IN ('ACTIVE', 'SUPERSEDED')) = (activated_at IS NOT NULL)");
        builder.HasCheck(
            "superseded",
            "(status = 'SUPERSEDED') = (superseded_by_target_version_id IS NOT NULL) AND (superseded_by_target_version_id IS NULL OR superseded_by_target_version_id <> id)");

        builder.HasOne<KpiTargetVersion>().WithMany().HasForeignKey(e => e.SupersededByTargetVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<KpiAssignment>().WithMany().HasForeignKey(e => e.KpiAssignmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
