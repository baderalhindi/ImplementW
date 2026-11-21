using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Infrastructure.Persistence.Configurations.IdentityAccess;

internal sealed class BannerImageConfiguration : IEntityTypeConfiguration<BannerImage>
{
    public void Configure(EntityTypeBuilder<BannerImage> builder)
    {
        builder.ToTable("banner_image", "identity_access", t =>
        {
            t.HasCheckConstraint("ck_banner_image_focal_point", "focal_x BETWEEN 0 AND 100 AND focal_y BETWEEN 0 AND 100");
            t.HasCheckConstraint("ck_banner_image_content_size", "octet_length(content) <= 307200");
        });
        builder.Property(e => e.Code).HasMaxLength(64);
        builder.HasIndex(e => e.Code).IsUnique();
        builder.Property(e => e.CaptionAr).HasMaxLength(200);
        builder.Property(e => e.CaptionEn).HasMaxLength(200);
        builder.Property(e => e.AltAr).HasMaxLength(500);
        builder.Property(e => e.AltEn).HasMaxLength(500);
        builder.Property(e => e.Credit).HasMaxLength(200);
        builder.Property(e => e.ContentType).HasMaxLength(50);
    }
}

internal sealed class UserBannerPreferenceConfiguration : IEntityTypeConfiguration<UserBannerPreference>
{
    public void Configure(EntityTypeBuilder<UserBannerPreference> builder)
    {
        builder.ToTable("user_banner_preference", "identity_access");
        builder.HasIndex(e => e.UserId).IsUnique();
        // The person's own choice: it goes with them when they are deleted.
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
