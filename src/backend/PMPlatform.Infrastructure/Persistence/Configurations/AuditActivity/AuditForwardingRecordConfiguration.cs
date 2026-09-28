using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.AuditActivity;

namespace PMPlatform.Infrastructure.Persistence.Configurations.AuditActivity;

/// <remarks>
/// <c>invocation_id</c> has no foreign key yet: its target, <c>integration_monitoring.invocation</c>, is TASK-075's and
/// does not exist. That task adds the constraint (record F-3).
/// </remarks>
internal sealed class AuditForwardingRecordConfiguration : IEntityTypeConfiguration<AuditForwardingRecord>
{
    public void Configure(EntityTypeBuilder<AuditForwardingRecord> builder)
    {
        builder.ToTable("audit_forwarding_record", "audit_activity");
        builder.HasIndex(e => e.AuditEventId).IsUnique();

        builder.HasOne<AuditEvent>().WithMany().HasForeignKey(e => e.AuditEventId).OnDelete(DeleteBehavior.Restrict);

        // indexing-strategy.md I-56: the forwarder's queue, oldest unforwarded first.
        builder.HasIndex(e => new { e.Status, e.CreatedAt });
    }
}
