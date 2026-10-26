using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.Approval;
using PMPlatform.Domain.ChangeRequest;
using PMPlatform.Domain.IdentityAccess;
using ChangeRequestEntity = PMPlatform.Domain.ChangeRequest.ChangeRequest;

namespace PMPlatform.Infrastructure.Persistence.Configurations.ChangeRequest;

internal sealed class ChangeAuthorizationConfiguration : IEntityTypeConfiguration<ChangeAuthorization>
{
    public void Configure(EntityTypeBuilder<ChangeAuthorization> builder)
    {
        builder.ToTable("change_authorization", "change_request");

        // Its row version serialises its application: two target modules applying it at once cannot both succeed.
        builder.HasRowVersion();
        builder.Property(e => e.TargetModule).HasMaxLength(50);
        builder.Property(e => e.TargetType).HasMaxLength(100);
        builder.Property(e => e.IdempotencyKey).HasMaxLength(200);
        builder.Property(e => e.AppliedReference).HasMaxLength(200);

        // ERD: exactly-once issuance under redelivery.
        builder.HasIndex(e => e.IdempotencyKey).IsUnique();
        builder.HasIndex(e => new { e.ChangeRequestId, e.IssuedAt });

        builder.HasCheck("target_revision_no", "target_revision_no >= 1");
        builder.HasCheck("expires_at", "expires_at IS NULL OR expires_at > issued_at");

        // APPLIED exactly when it says when, by whom and through which record of its target module.
        builder.HasCheck(
            "applied",
            "(status = 'APPLIED') = (applied_at IS NOT NULL) AND (status = 'APPLIED') = (applied_by_user_id IS NOT NULL) "
            + "AND (status = 'APPLIED') = (applied_reference IS NOT NULL)");

        builder.HasOne<ChangeRequestEntity>().WithMany().HasForeignKey(e => e.ChangeRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApprovalInstance>().WithMany().HasForeignKey(e => e.ApprovalInstanceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.AppliedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
