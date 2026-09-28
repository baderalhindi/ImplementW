using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.AuditActivity;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Domain.MasterDataConfig;
using ProjectEntity = PMPlatform.Domain.Project.Project;

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

        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ScopeProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ExternalEntity>().WithMany().HasForeignKey(e => e.ScopeExternalEntityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MasterDataItem>().WithMany().HasForeignKey(e => e.DataClassificationItemId).OnDelete(DeleteBehavior.Restrict);

        // indexing-strategy.md I-49 (SCR-058), I-50 and I-51 (ADM-050), built with the table; I-55, the head of the hash chain.
        builder.HasIndex(e => new { e.ScopeProjectId, e.OccurredAt, e.Id });
        builder.HasIndex(e => new { e.OccurredAt, e.Id });
        builder.HasIndex(e => new { e.ActorUserId, e.OccurredAt });
        builder.HasIndex(e => new { e.RecordedAt, e.Id });
    }
}
