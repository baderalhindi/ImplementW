using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.AuditActivity;

namespace PMPlatform.Infrastructure.Persistence.Configurations.AuditActivity;

internal sealed class AuditEventAttributeConfiguration : IEntityTypeConfiguration<AuditEventAttribute>
{
    public void Configure(EntityTypeBuilder<AuditEventAttribute> builder)
    {
        builder.ToTable("audit_event_attribute", "audit_activity");
        builder.IsAppendOnly();
        builder.Property(e => e.AttributeName).HasMaxLength(100);
        builder.HasIndex(e => new { e.AuditEventId, e.AttributeName }).IsUnique();

        builder.HasOne<AuditEvent>().WithMany().HasForeignKey(e => e.AuditEventId).OnDelete(DeleteBehavior.Restrict);
    }
}
