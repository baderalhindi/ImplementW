using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Domain.Risk;
using RiskEntity = PMPlatform.Domain.Risk.Risk;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Risk;

internal sealed class RiskTreatmentActionConfiguration : IEntityTypeConfiguration<RiskTreatmentAction>
{
    public void Configure(EntityTypeBuilder<RiskTreatmentAction> builder)
    {
        builder.ToTable("risk_treatment_action", "risk");
        builder.HasRowVersion();
        builder.HasNarrative(e => e.Title, "title");
        builder.HasNarrative(e => e.Description, "description");

        builder.HasCheck("completed", "(status = 'COMPLETED') = (completed_at IS NOT NULL)");

        builder.HasOne<RiskEntity>().WithMany().HasForeignKey(e => e.RiskId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
