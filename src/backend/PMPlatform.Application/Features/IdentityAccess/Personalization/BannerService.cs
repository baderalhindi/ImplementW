using PMPlatform.Application.Features.IdentityAccess.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Personalization;

/// <summary>
/// The Home welcome banner (review round 8, point 07): the images the platform holds, and each person's choice — the green banner, one image, or images
/// that take turns. A person with no choice saved sees the green banner. Anyone signed in reads the images and sets their own choice; adding an image
/// is the master data administrators' (MASTER_DATA_MANAGE, checked by the API).
/// </summary>
internal sealed class BannerService(IBannerRepository repository, TimeProvider timeProvider) : IBannerService
{
    /// <summary>The PDF's limit: a compressed WebP of at most 300 KB.</summary>
    public const int MaxContentBytes = 300 * 1024;

    private const string WebP = "image/webp";

    public async Task<BannerImageList> ListImagesAsync(CancellationToken cancellationToken) =>
        new(await repository.ListImagesAsync(cancellationToken).ConfigureAwait(false));

    public Task<BannerImageContent?> ReadContentAsync(Guid imageId, CancellationToken cancellationToken) =>
        repository.ReadContentAsync(imageId, cancellationToken);

    public async Task<BannerPreferenceDetail> GetPreferenceAsync(Guid callerId, CancellationToken cancellationToken) =>
        await repository.FindPreferenceAsync(callerId, cancellationToken).ConfigureAwait(false) is { } saved
            ? new BannerPreferenceDetail(saved.Style, saved.ImageIds)
            : new BannerPreferenceDetail(BannerStyle.Default, []);

    public async Task<AdministrationResult<BannerPreferenceDetail>> SetPreferenceAsync(
        Guid callerId, BannerStyle style, IReadOnlyList<Guid> imageIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(imageIds);
        HashSet<Guid> known = [.. (await repository.ListImagesAsync(cancellationToken).ConfigureAwait(false)).Select(i => i.Id)];
        List<Guid> chosen = [.. imageIds.Distinct()];
        if (chosen.Any(id => !known.Contains(id)))
        {
            return AdministrationError.Rule(IdentityAccessErrorCodes.BannerImagesInvalid, new FieldIssue("imageIds", FieldIssue.NotFound));
        }

        if ((style == BannerStyle.Image && chosen.Count != 1) || (style == BannerStyle.Rotate && chosen.Count == 0))
        {
            return AdministrationError.Rule(IdentityAccessErrorCodes.BannerImagesInvalid, new FieldIssue("imageIds", FieldIssue.Required));
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        UserBannerPreference? preference = await repository.FindPreferenceAsync(callerId, cancellationToken).ConfigureAwait(false);
        if (preference is null)
        {
            preference = new UserBannerPreference { Id = Guid.CreateVersion7(now), UserId = callerId, CreatedAt = now, CreatedBy = callerId };
            repository.Add(preference);
        }

        preference.Style = style;
        preference.ImageIds = chosen;
        preference.UpdatedAt = now;
        preference.UpdatedBy = callerId;
        await repository.SaveAsync(cancellationToken).ConfigureAwait(false);
        return new BannerPreferenceDetail(preference.Style, preference.ImageIds);
    }

    public async Task<AdministrationResult<BannerImageSummary>> SaveImageAsync(Guid callerId, BannerImageUpload upload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(upload);
        if (upload.ContentType != WebP || !IsWebP(upload.Content))
        {
            return AdministrationError.UnsupportedMediaType;
        }

        if (upload.Content.Length > MaxContentBytes)
        {
            return AdministrationError.PayloadTooLarge;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        BannerImage? image = await repository.FindImageByCodeAsync(upload.Code, cancellationToken).ConfigureAwait(false);
        if (image is null)
        {
            image = new BannerImage
            {
                Id = Guid.CreateVersion7(now),
                Code = upload.Code,
                CaptionAr = upload.Caption.Ar,
                CaptionEn = upload.Caption.En,
                AltAr = upload.Alt.Ar,
                AltEn = upload.Alt.En,
                ContentType = upload.ContentType,
                Content = upload.Content,
                CreatedAt = now,
                CreatedBy = callerId,
            };
            repository.Add(image);
        }

        image.CaptionAr = upload.Caption.Ar;
        image.CaptionEn = upload.Caption.En;
        image.AltAr = upload.Alt.Ar;
        image.AltEn = upload.Alt.En;
        image.FocalX = upload.FocalX;
        image.FocalY = upload.FocalY;
        image.Credit = upload.Credit;
        image.ContentType = upload.ContentType;
        image.Content = upload.Content;
        image.Width = upload.Width;
        image.Height = upload.Height;
        image.SortOrder = upload.SortOrder;
        image.UpdatedAt = now;
        image.UpdatedBy = callerId;
        await repository.SaveAsync(cancellationToken).ConfigureAwait(false);
        return new BannerImageSummary(
            image.Id, image.Code, upload.Caption, upload.Alt, image.FocalX, image.FocalY, image.Credit, image.Width, image.Height);
    }

    /// <summary>A RIFF container whose form is WEBP: the content is what its type says.</summary>
    private static bool IsWebP(byte[] content) =>
        content.Length > 12
        && content[0] == (byte)'R' && content[1] == (byte)'I' && content[2] == (byte)'F' && content[3] == (byte)'F'
        && content[8] == (byte)'W' && content[9] == (byte)'E' && content[10] == (byte)'B' && content[11] == (byte)'P';
}
