using PMPlatform.Domain.Common;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Contracts;

/// <summary>An image the welcome banner can show, without its content, which is read from its own address.</summary>
public sealed record BannerImageSummary(
    Guid Id, string Code, BilingualLabel Caption, BilingualLabel Alt, short FocalX, short FocalY, string? Credit, int Width, int Height);

public sealed record BannerImageList(IReadOnlyList<BannerImageSummary> Items);

/// <summary>An image's bytes and type, for the browser to show and cache.</summary>
public sealed record BannerImageContent(string ContentType, byte[] Content, DateTimeOffset UpdatedAt);

/// <summary>One person's banner: the style and the images chosen, in the order they show.</summary>
public sealed record BannerPreferenceDetail(BannerStyle Style, IReadOnlyList<Guid> ImageIds);

/// <summary>An image to add, or to replace when its code is already used: a WebP of at most 300 KB and its words.</summary>
public sealed record BannerImageUpload(
    string Code, BilingualLabel Caption, BilingualLabel Alt, short FocalX, short FocalY, string? Credit, int Width, int Height, int SortOrder, string ContentType,
    byte[] Content);

public interface IBannerService
{
    public Task<BannerImageList> ListImagesAsync(CancellationToken cancellationToken);

    public Task<BannerImageContent?> ReadContentAsync(Guid imageId, CancellationToken cancellationToken);

    public Task<BannerPreferenceDetail> GetPreferenceAsync(Guid callerId, CancellationToken cancellationToken);

    /// <summary>The image styles need images that exist; one image takes exactly one.</summary>
    public Task<AdministrationResult<BannerPreferenceDetail>> SetPreferenceAsync(
        Guid callerId, BannerStyle style, IReadOnlyList<Guid> imageIds, CancellationToken cancellationToken);

    public Task<AdministrationResult<BannerImageSummary>> SaveImageAsync(Guid callerId, BannerImageUpload upload, CancellationToken cancellationToken);
}
