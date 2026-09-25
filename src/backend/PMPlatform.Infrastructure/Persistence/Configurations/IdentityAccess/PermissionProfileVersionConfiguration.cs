using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Infrastructure.Persistence.Configurations.IdentityAccess;

internal sealed class PermissionProfileVersionConfiguration : IEntityTypeConfiguration<PermissionProfileVersion>
{
    public void Configure(EntityTypeBuilder<PermissionProfileVersion> builder)
    {
        builder.ToTable("permission_profile_version", "identity_access");
        builder.HasIndex(e => new { e.PermissionProfileId, e.VersionNo }).IsUnique();
        builder.HasNarrative(e => e.ChangeSummary, "change_summary");
        builder.HasGovernedLifecycle();

        builder.HasOne<PermissionProfile>().WithMany().HasForeignKey(e => e.PermissionProfileId).OnDelete(DeleteBehavior.Restrict);
    }
}
