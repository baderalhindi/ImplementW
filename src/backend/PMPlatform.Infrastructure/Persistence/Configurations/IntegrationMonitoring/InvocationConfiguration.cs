using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.IntegrationMonitoring;

namespace PMPlatform.Infrastructure.Persistence.Configurations.IntegrationMonitoring;

internal sealed class InvocationConfiguration : IEntityTypeConfiguration<Invocation>
{
    /// <summary>Named: the generated name exceeds PostgreSQL's 63 characters. One attempt per number in a lineage.</summary>
    public const string LineageKey = "ix_invocation_lineage_attempt";

    public void Configure(EntityTypeBuilder<Invocation> builder)
    {
        builder.ToTable("invocation", "integration_monitoring");
        builder.Property(e => e.OperationCode).HasMaxLength(100);
        builder.Property(e => e.IdempotencyKey).HasMaxLength(200);
        builder.Property(e => e.AttemptNo).HasDatabaseDefault((short)1);
        builder.Property(e => e.SourceModule).HasMaxLength(50);
        builder.Property(e => e.SourceReference).HasMaxLength(200);
        builder.Property(e => e.ResponseCode).HasMaxLength(50);
        builder.HasCheck("attempt_no", "attempt_no >= 1");
        builder.HasCheck("completed", "(status = 'IN_PROGRESS') = (completed_at IS NULL)");
        builder.HasCheck("next_attempt", "(status = 'RETRY_SCHEDULED') = (next_attempt_at IS NOT NULL)");
        builder.HasCheck("failure", "(status IN ('IN_PROGRESS', 'SUCCEEDED')) = (failure_category IS NULL)");

        builder.HasIndex(e => new { e.IntegrationInstanceId, e.OperationCode, e.IdempotencyKey, e.AttemptNo }).IsUnique().HasDatabaseName(LineageKey);

        // indexing-strategy.md I-57: ADM-048 and the metrics, an instance's attempts in a time window, newest first. I-58: a lineage traced by its correlation id.
        builder.HasIndex(e => new { e.IntegrationInstanceId, e.StartedAt, e.Id });
        builder.HasIndex(e => e.CorrelationId);

        // The retry pass's queue: lineages whose next attempt is due, earliest first (a worker query, indexed by its owner).
        builder.HasIndex(e => new { e.NextAttemptAt, e.Id }).HasFilter("status = 'RETRY_SCHEDULED'").HasDatabaseName("ix_invocation_retry_queue");

        builder.HasOne<IntegrationInstance>().WithMany().HasForeignKey(e => e.IntegrationInstanceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SyncRun>().WithMany().HasForeignKey(e => e.SyncRunId).OnDelete(DeleteBehavior.Restrict);
    }
}
