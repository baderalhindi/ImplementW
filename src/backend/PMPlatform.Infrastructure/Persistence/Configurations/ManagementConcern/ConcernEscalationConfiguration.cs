using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Domain.ManagementConcern;
using ConcernEntity = PMPlatform.Domain.ManagementConcern.ManagementConcern;

namespace PMPlatform.Infrastructure.Persistence.Configurations.ManagementConcern;

internal sealed class ConcernEscalationConfiguration : IEntityTypeConfiguration<ConcernEscalation>
{
    public void Configure(EntityTypeBuilder<ConcernEscalation> builder)
    {
        builder.ToTable("concern_escalation", "management_concern");
        builder.HasRowVersion();
        builder.HasNarrative(e => e.Reason, "reason");
        builder.HasNarrative(e => e.Resolution, "resolution");

        // ERD: escalations are numbered within their concern.
        builder.HasIndex(e => new { e.ManagementConcernId, e.EscalationNo }).IsUnique();

        // One OPEN escalation per concern at a time (WF-07 §5.3, "the configured number of concurrent open escalations": one).
        builder.HasIndex(e => e.ManagementConcernId).IsUnique().HasFilter("status = 'OPEN'").HasDatabaseName("ix_concern_escalation_one_open");

        // R-37: an Idempotency-Key is the escalator's; it raises one escalation, which a retry finds.
        builder.HasIndex(e => new { e.EscalatedByUserId, e.RequestKey }).IsUnique();

        // TASK-026 (indexing-strategy.md I-20, I-21): SCR-087 Escalations, open first, and the escalations addressed to a role.
        builder.HasIndex(e => new { e.Status, e.EscalatedAt, e.Id });
        builder.HasIndex(e => new { e.EscalatedToRoleId, e.Status });

        builder.HasCheck("escalation_no", "escalation_no >= 1");

        // An escalation ends once — resolved by its addressee with the direction given, or withdrawn by its escalator — and says by whom.
        builder.HasCheck("ended",
            "(status = 'OPEN') = (resolved_at IS NULL) AND (status = 'OPEN') = (resolved_by_user_id IS NULL) AND (status = 'RESOLVED') = (resolution IS NOT NULL)");

        builder.HasOne<ConcernEntity>().WithMany().HasForeignKey(e => e.ManagementConcernId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.EscalatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ResolvedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Role>().WithMany().HasForeignKey(e => e.EscalatedToRoleId).OnDelete(DeleteBehavior.Restrict);
    }
}
