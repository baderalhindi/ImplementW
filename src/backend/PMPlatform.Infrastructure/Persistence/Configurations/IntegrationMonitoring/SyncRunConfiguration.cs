using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.IntegrationMonitoring;

namespace PMPlatform.Infrastructure.Persistence.Configurations.IntegrationMonitoring;

internal sealed class SyncRunConfiguration : IEntityTypeConfiguration<SyncRun>
{
    public void Configure(EntityTypeBuilder<SyncRun> builder)
    {
        builder.ToTable("sync_run", "integration_monitoring");
        builder.Property(e => e.RecordsRead).HasDatabaseDefault(0);
        builder.Property(e => e.RecordsApplied).HasDatabaseDefault(0);
        builder.Property(e => e.RecordsRejected).HasDatabaseDefault(0);
        builder.Property(e => e.MismatchCount).HasDatabaseDefault(0);
        builder.HasCheck("counts", "records_read >= 0 AND records_applied >= 0 AND records_rejected >= 0 AND mismatch_count >= 0");
        builder.HasCheck("completed", "(status = 'RUNNING') = (completed_at IS NULL)");

        // indexing-strategy.md I-60: ADM-047, an instance's runs, newest first.
        builder.HasIndex(e => new { e.IntegrationInstanceId, e.StartedAt, e.Id });

        builder.HasOne<IntegrationInstance>().WithMany().HasForeignKey(e => e.IntegrationInstanceId).OnDelete(DeleteBehavior.Restrict);
    }
}
