using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Domain.Risk;
using RiskEntity = PMPlatform.Domain.Risk.Risk;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Risk;

internal sealed class RiskAcceptanceConfiguration : IEntityTypeConfiguration<RiskAcceptance>
{
    public void Configure(EntityTypeBuilder<RiskAcceptance> builder)
    {
        builder.ToTable("risk_acceptance", "risk");
        builder.HasRowVersion();
        builder.HasNarrative(e => e.Rationale, "rationale");

        // At most one ACTIVE acceptance per risk; and the expiry pass finds the lapsed ones by their date.
        builder.HasIndex(e => e.RiskId).IsUnique().HasFilter("status = 'ACTIVE'").HasDatabaseName("ix_risk_acceptance_active_risk_id");
        builder.HasIndex(e => new { e.Status, e.ExpiresOn });

        // No permanent acceptance (TASK-055 gate decision): it expires after the day it was given.
        builder.HasCheck("expires_on", "expires_on > (accepted_at AT TIME ZONE 'UTC')::date");
        builder.HasCheck("revoked", "(status = 'REVOKED') = (revoked_at IS NOT NULL)");

        builder.HasOne<RiskEntity>().WithMany().HasForeignKey(e => e.RiskId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.AcceptedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
