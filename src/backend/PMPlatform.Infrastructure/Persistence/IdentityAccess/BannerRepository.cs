using Microsoft.EntityFrameworkCore;
using PMPlatform.Application.Features.IdentityAccess.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Personalization;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Infrastructure.Persistence.IdentityAccess;

/// <summary>The banner images and each person's banner choice (review round 8, point 07). The image list never reads the content.</summary>
internal sealed class BannerRepository(PMPlatformDbContext context) : IBannerRepository
{
    public async Task<IReadOnlyList<BannerImageSummary>> ListImagesAsync(CancellationToken cancellationToken) =>
        [.. (await context.Set<BannerImage>().AsNoTracking()
                .OrderBy(i => i.SortOrder).ThenBy(i => i.Code)
                .Select(i => new { i.Id, i.Code, i.CaptionAr, i.CaptionEn, i.AltAr, i.AltEn, i.FocalX, i.FocalY, i.Credit, i.Width, i.Height })
                .ToListAsync(cancellationToken).ConfigureAwait(false))
            .Select(i => new BannerImageSummary(
                i.Id, i.Code, new BilingualLabel(i.CaptionAr, i.CaptionEn), new BilingualLabel(i.AltAr, i.AltEn), i.FocalX, i.FocalY, i.Credit, i.Width, i.Height))];

    public Task<BannerImageContent?> ReadContentAsync(Guid imageId, CancellationToken cancellationToken) =>
        context.Set<BannerImage>().AsNoTracking().Where(i => i.Id == imageId)
            .Select(i => new BannerImageContent(i.ContentType, i.Content, i.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<UserBannerPreference?> FindPreferenceAsync(Guid userId, CancellationToken cancellationToken) =>
        context.Set<UserBannerPreference>().SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);

    public Task<BannerImage?> FindImageByCodeAsync(string code, CancellationToken cancellationToken) =>
        context.Set<BannerImage>().SingleOrDefaultAsync(i => i.Code == code, cancellationToken);

    public void Add(UserBannerPreference preference) => context.Add(preference);

    public void Add(BannerImage image) => context.Add(image);

    public Task SaveAsync(CancellationToken cancellationToken) => context.SaveChangesAsync(cancellationToken);
}
