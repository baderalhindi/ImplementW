using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.Progress;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Progress;

internal sealed class ProgressStageEntryConfiguration : IEntityTypeConfiguration<ProgressStageEntry>
{
    public void Configure(EntityTypeBuilder<ProgressStageEntry> builder)
    {
        builder.ToTable("progress_stage_entry", "progress");
        builder.Property(e => e.StageCode).HasMaxLength(50);
        builder.Property(e => e.WeightPercent).HasPrecision(5, 2);
        builder.Property(e => e.PlannedPercent).HasPrecision(18, 4);
        builder.Property(e => e.PercentComplete).HasPrecision(18, 4);
        builder.Property(e => e.Note).HasMaxLength(200);
        builder.HasNarrative(e => e.DecreaseReason, "decrease_reason");
        builder.HasCheck(
            "percent",
            "weight_percent BETWEEN 0 AND 100 AND (planned_percent IS NULL OR planned_percent BETWEEN 0 AND 100) "
            + "AND (percent_complete IS NULL OR percent_complete BETWEEN 0 AND 100)");
        builder.HasCheck("actual_dates", "actual_start_date IS NULL OR actual_finish_date IS NULL OR actual_start_date <= actual_finish_date");

        // A period's stages are read whole and go with it: a returned revision's next is a copy, a deleted draft takes its stages along.
        builder.HasOne<ProgressSubmission>().WithMany().HasForeignKey(e => e.ProgressSubmissionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.ProgressSubmissionId, e.StageCode }).IsUnique();
    }
}
