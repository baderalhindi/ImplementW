using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Infrastructure.Persistence.Configurations.IdentityAccess;

internal sealed class PermissionProfileConfiguration : IEntityTypeConfiguration<PermissionProfile>
{
    public void Configure(EntityTypeBuilder<PermissionProfile> builder)
    {
        builder.ToTable("permission_profile", "identity_access");
        builder.Property(e => e.Code).HasMaxLength(50);
        builder.HasIndex(e => e.Code).IsUnique();
        builder.HasBilingualLabel(e => e.Name, "name");
        builder.Property(e => e.IsShippedDefault).HasDatabaseDefault(false);

        builder.HasOne<Role>().WithMany().HasForeignKey(e => e.BaseRoleId).OnDelete(DeleteBehavior.Restrict);
    }
}
