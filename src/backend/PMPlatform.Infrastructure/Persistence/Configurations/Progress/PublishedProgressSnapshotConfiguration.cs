using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Domain.MasterDataConfig;
using PMPlatform.Domain.Progress;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Progress;

internal sealed class PublishedProgressSnapshotConfiguration : IEntityTypeConfiguration<PublishedProgressSnapshot>
{
    public void Configure(EntityTypeBuilder<PublishedProgressSnapshot> builder)
    {
        builder.ToTable("published_progress_snapshot", "progress");
        builder.IsAppendOnly();
        builder.Property(e => e.ActualPercent).HasPrecision(18, 4);
        builder.Property(e => e.PlannedPercent).HasPrecision(18, 4);
        builder.HasIndex(e => e.ProgressSubmissionId).IsUnique();

        // One official record per period: publishing closes the period (progress-update.md D-6).
        builder.HasIndex(e => e.ReportingCycleId).IsUnique();

        // TASK-026 (indexing-strategy.md I-38): FG-01 reads a project's latest PUBLISHED/OFFICIAL snapshot and its trend.
        builder.HasIndex(e => new { e.ProjectId, e.PublishedAt });

        builder.HasCheck("percent", "actual_percent BETWEEN 0 AND 100 AND (planned_percent IS NULL OR planned_percent BETWEEN 0 AND 100)");

        builder.HasOne<ReportingCycle>().WithMany().HasForeignKey(e => e.ReportingCycleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProgressSubmission>().WithMany().HasForeignKey(e => e.ProgressSubmissionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.PublishedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ConfigurationVersion>().WithMany().HasForeignKey(e => e.HealthRuleConfigurationVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}
