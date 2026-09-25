using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Infrastructure.Persistence.Configurations.IdentityAccess;

internal sealed class ExternalEntityConfiguration : IEntityTypeConfiguration<ExternalEntity>
{
    public void Configure(EntityTypeBuilder<ExternalEntity> builder)
    {
        builder.ToTable("external_entity", "identity_access");
        builder.Property(e => e.Code).HasMaxLength(50);
        builder.HasIndex(e => e.Code).IsUnique();
        builder.HasBilingualLabel(e => e.Name, "name");

        builder.HasOne<User>().WithMany().HasForeignKey(e => e.SponsorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MasterDataItem>().WithMany().HasForeignKey(e => e.EntityTypeItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
