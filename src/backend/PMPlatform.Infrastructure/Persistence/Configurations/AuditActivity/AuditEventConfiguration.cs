using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.AuditActivity;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Infrastructure.Persistence.Configurations.AuditActivity;

internal sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("audit_event", "audit_activity");
        builder.IsAppendOnly();
        builder.Property(e => e.EventType).HasMaxLength(100);
        builder.Property(e => e.SubjectModule).HasMaxLength(50);
        builder.Property(e => e.SubjectType).HasMaxLength(100);

        // TASK-033: the database places each event in the hash chain on insert (TASK-033 migration), so these are never
        // written by the application and are read back after the insert.
        builder.Property(e => e.RecordedAt).ValueGeneratedOnAdd();
        builder.Property(e => e.PreviousEventHash).HasColumnType("char(64)").ValueGeneratedOnAdd();
        builder.Property(e => e.EventHash).HasColumnType("char(64)").IsRequired().ValueGeneratedOnAdd();

        // TASK-073: written by ingestion and covered by the hash, so a changed attribute breaks the chain (TASK-033 F-2).
        builder.Property(e => e.AttributeDigest).HasColumnType("char(64)");

        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        // No foreign key to the project (R4-5): the trail is append-only and outlives a project removed by the demo reset or an import rollback.
        // Nor to the external entity or the data classification (2026-11-14): the administration deletes them, and the trail keeps their ids.
        builder.HasIndex(e => e.ScopeExternalEntityId);
        builder.HasIndex(e => e.DataClassificationItemId);

        // indexing-strategy.md I-49 (SCR-058), I-50 and I-51 (ADM-050), built with the table; I-55, the head of the hash chain.
        builder.HasIndex(e => new { e.ScopeProjectId, e.OccurredAt, e.Id });
        builder.HasIndex(e => new { e.OccurredAt, e.Id });
        builder.HasIndex(e => new { e.ActorUserId, e.OccurredAt });
        builder.HasIndex(e => new { e.RecordedAt, e.Id });
    }
}
