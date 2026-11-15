using PMPlatform.Application.Features.IdentityAccess.Contracts;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Personalization;

/// <summary>The <c>identity_access.banner_image</c> and <c>identity_access.user_banner_preference</c> tables (review round 8, point 07).</summary>
public interface IBannerRepository
{
    public Task<IReadOnlyList<BannerImageSummary>> ListImagesAsync(CancellationToken cancellationToken);

    public Task<BannerImageContent?> ReadContentAsync(Guid imageId, CancellationToken cancellationToken);

    public Task<UserBannerPreference?> FindPreferenceAsync(Guid userId, CancellationToken cancellationToken);

    public Task<BannerImage?> FindImageByCodeAsync(string code, CancellationToken cancellationToken);

    public void Add(UserBannerPreference preference);

    public void Add(BannerImage image);

    public Task SaveAsync(CancellationToken cancellationToken);
}
