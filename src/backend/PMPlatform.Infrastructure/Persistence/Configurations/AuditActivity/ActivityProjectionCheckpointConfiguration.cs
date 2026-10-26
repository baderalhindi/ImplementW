using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.AuditActivity;

namespace PMPlatform.Infrastructure.Persistence.Configurations.AuditActivity;

internal sealed class ActivityProjectionCheckpointConfiguration : IEntityTypeConfiguration<ActivityProjectionCheckpoint>
{
    public void Configure(EntityTypeBuilder<ActivityProjectionCheckpoint> builder)
    {
        builder.ToTable("activity_projection_checkpoint", "audit_activity");

        // A position names both parts or neither.
        builder.HasCheck("position", "(last_recorded_at IS NULL) = (last_audit_event_id IS NULL)");
    }
}
