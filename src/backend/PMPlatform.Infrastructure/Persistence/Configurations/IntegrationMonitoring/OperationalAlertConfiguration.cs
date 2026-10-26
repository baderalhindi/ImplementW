using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Domain.IntegrationMonitoring;

namespace PMPlatform.Infrastructure.Persistence.Configurations.IntegrationMonitoring;

internal sealed class OperationalAlertConfiguration : IEntityTypeConfiguration<OperationalAlert>
{
    /// <summary>One live alert per instance and condition (BR-INT-036): a provider outage is one alert, not one per failed call.</summary>
    public const string LiveAlertKey = "ix_operational_alert_live";

    public void Configure(EntityTypeBuilder<OperationalAlert> builder)
    {
        builder.ToTable("operational_alert", "integration_monitoring");
        builder.HasNarrative(e => e.ResolutionNote, "resolution_note");
        builder.HasCheck("acknowledged", "(acknowledged_by_user_id IS NULL) = (acknowledged_at IS NULL) AND (status <> 'ACKNOWLEDGED' OR acknowledged_at IS NOT NULL)");
        builder.HasCheck("resolved", "(status IN ('RESOLVED', 'DISMISSED')) = (resolved_at IS NOT NULL) AND (status IN ('RESOLVED', 'DISMISSED')) = (resolution_note IS NOT NULL)");

        builder.HasIndex(e => new { e.IntegrationInstanceId, e.AlertType }).IsUnique().HasFilter("status IN ('OPEN', 'ACKNOWLEDGED')").HasDatabaseName(LiveAlertKey);

        // indexing-strategy.md I-59: ADM-047, alerts by state, newest first.
        builder.HasIndex(e => new { e.Status, e.RaisedAt, e.Id });

        builder.HasOne<IntegrationInstance>().WithMany().HasForeignKey(e => e.IntegrationInstanceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SyncRun>().WithMany().HasForeignKey(e => e.SyncRunId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Invocation>().WithMany().HasForeignKey(e => e.InvocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.AcknowledgedByUserId).OnDelete(DeleteBehavior.Restrict);

        // Acknowledge, resolve and dismiss race; the second of two decisions cannot commit.
        builder.HasRowVersion();
    }
}
