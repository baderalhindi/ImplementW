using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.Approval;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Approval;

internal sealed class ApprovalDelegationConfiguration : IEntityTypeConfiguration<ApprovalDelegation>
{
    public void Configure(EntityTypeBuilder<ApprovalDelegation> builder)
    {
        builder.ToTable("approval_delegation", "approval");
        builder.Property(e => e.RoutingKey).HasMaxLength(100);
        builder.HasCheck("period", "valid_to > valid_from");

        builder.HasOne<User>().WithMany().HasForeignKey(e => e.DelegatorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.DelegateUserId).OnDelete(DeleteBehavior.Restrict);

        // indexing-strategy.md I-25: SCR-114, and the delegations to a user when their authority is resolved.
        builder.HasIndex(e => new { e.DelegateUserId, e.Status, e.ValidTo });

        builder.HasRowVersion();
    }
}
