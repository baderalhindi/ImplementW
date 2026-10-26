using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.FinancialKpi;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Infrastructure.Persistence.Configurations.FinancialKpi;

internal sealed class KpiMeasurementConfiguration : IEntityTypeConfiguration<KpiMeasurement>
{
    public void Configure(EntityTypeBuilder<KpiMeasurement> builder)
    {
        builder.ToTable("kpi_measurement", "financial_kpi");
        builder.HasRowVersion();
        builder.Property(e => e.MeasuredValue).HasPrecision(18, 4);
        builder.HasNarrative(e => e.Narrative, "narrative");
        builder.HasIndex(e => new { e.KpiAssignmentId, e.PeriodStart }).IsUnique();
        builder.HasIndex(e => e.KpiTargetVersionId);

        builder.HasCheck("period", "period_end >= period_start");

        // TASK-052: a value is present exactly when MEASURED, never a 0 standing in for Unknown; a rating needs a value.
        builder.HasCheck("value_status", "(value_status = 'MEASURED') = (measured_value IS NOT NULL)");
        builder.HasCheck(
            "rag_status",
            "(value_status = 'NOT_APPLICABLE') = (rag_status = 'NOT_APPLICABLE') AND (rag_status NOT IN ('GREEN', 'AMBER', 'RED') OR measured_value IS NOT NULL)");
        builder.HasCheck("submitted", "(status = 'DRAFT') = (submitted_at IS NULL)");
        builder.HasCheck("published", "(status = 'PUBLISHED') = (published_at IS NOT NULL) AND (published_at IS NULL) = (published_by_user_id IS NULL)");

        builder.HasOne<KpiAssignment>().WithMany().HasForeignKey(e => e.KpiAssignmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<KpiTargetVersion>().WithMany().HasForeignKey(e => e.KpiTargetVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.PublishedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
