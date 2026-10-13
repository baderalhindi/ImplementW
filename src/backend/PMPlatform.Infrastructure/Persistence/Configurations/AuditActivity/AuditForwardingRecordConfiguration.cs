using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.AuditActivity;
using PMPlatform.Domain.IntegrationMonitoring;

namespace PMPlatform.Infrastructure.Persistence.Configurations.AuditActivity;

/// <remarks>
/// <c>invocation_id</c> references <c>integration_monitoring.invocation</c> since TASK-075 built it (TASK-033 record F-3). The column
/// stays null: SIEM forwarding is AuditActivity's and no ADR-003 edge carries its telemetry to IntegrationMonitoring yet.
/// </remarks>
internal sealed class AuditForwardingRecordConfiguration : IEntityTypeConfiguration<AuditForwardingRecord>
{
    public void Configure(EntityTypeBuilder<AuditForwardingRecord> builder)
    {
        builder.ToTable("audit_forwarding_record", "audit_activity");
        builder.HasIndex(e => e.AuditEventId).IsUnique();

        builder.HasOne<AuditEvent>().WithMany().HasForeignKey(e => e.AuditEventId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Invocation>().WithMany().HasForeignKey(e => e.InvocationId).OnDelete(DeleteBehavior.Restrict);

        // indexing-strategy.md I-56: the forwarder's queue, oldest unforwarded first.
        builder.HasIndex(e => new { e.Status, e.CreatedAt });
    }
}
