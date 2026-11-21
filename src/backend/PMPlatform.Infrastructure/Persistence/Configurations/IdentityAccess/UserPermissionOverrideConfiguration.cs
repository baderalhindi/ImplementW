using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Infrastructure.Persistence.Configurations.IdentityAccess;

internal sealed class UserPermissionOverrideConfiguration : IEntityTypeConfiguration<UserPermissionOverride>
{
    public void Configure(EntityTypeBuilder<UserPermissionOverride> builder)
    {
        builder.ToTable("user_permission_override", "identity_access", table =>
            table.HasCheckConstraint("ck_user_permission_override_scope", "(effect = 'GRANT') = (data_scope IS NOT NULL)"));
        builder.HasIndex(e => new { e.UserId, e.PermissionId }).IsUnique();
        builder.HasNarrative(e => e.Reason, "reason");

        builder.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Permission>().WithMany().HasForeignKey(e => e.PermissionId).OnDelete(DeleteBehavior.Restrict);
    }
}
