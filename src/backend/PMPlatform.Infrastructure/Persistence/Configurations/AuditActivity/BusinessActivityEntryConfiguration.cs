using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.AuditActivity;
using PMPlatform.Domain.IdentityAccess;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Infrastructure.Persistence.Configurations.AuditActivity;

internal sealed class BusinessActivityEntryConfiguration : IEntityTypeConfiguration<BusinessActivityEntry>
{
    public void Configure(EntityTypeBuilder<BusinessActivityEntry> builder)
    {
        builder.ToTable("business_activity_entry", "audit_activity");
        builder.Property(e => e.ActivityKind).HasMaxLength(50);
        builder.Property(e => e.SummaryAr).HasMaxLength(500);
        builder.Property(e => e.SummaryEn).HasMaxLength(500);

        // One line per event at most: a rebuild repairs a line, it never adds a second.
        builder.HasOne<AuditEvent>().WithOne().HasForeignKey<BusinessActivityEntry>(e => e.AuditEventId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ActorUserId).OnDelete(DeleteBehavior.Restrict);

        // indexing-strategy.md I-48: SCR-057 Project Activity History, newest first.
        builder.HasIndex(e => new { e.ProjectId, e.OccurredAt, e.Id });
    }
}
