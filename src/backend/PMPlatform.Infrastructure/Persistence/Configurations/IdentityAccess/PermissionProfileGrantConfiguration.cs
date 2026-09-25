using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Infrastructure.Persistence.Configurations.IdentityAccess;

internal sealed class PermissionProfileGrantConfiguration : IEntityTypeConfiguration<PermissionProfileGrant>
{
    public void Configure(EntityTypeBuilder<PermissionProfileGrant> builder)
    {
        builder.ToTable("permission_profile_grant", "identity_access");
        builder.HasIndex(e => new { e.PermissionProfileVersionId, e.PermissionId }).IsUnique();

        builder.HasOne<PermissionProfileVersion>().WithMany().HasForeignKey(e => e.PermissionProfileVersionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Permission>().WithMany().HasForeignKey(e => e.PermissionId).OnDelete(DeleteBehavior.Restrict);
    }
}
