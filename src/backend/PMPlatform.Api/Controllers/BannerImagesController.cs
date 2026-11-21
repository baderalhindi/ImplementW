using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Models.DocumentManagement;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// The images the Home welcome banner can show (review round 8, point 07), kept in the database. Anyone signed in lists them and reads their content;
/// a master data administrator adds one, or replaces it by uploading again under the same code.
/// </summary>
[Route("api/v1/banner-images")]
[Tags("IdentityAccess")]
public sealed class BannerImagesController(IBannerService banners) : AdministrationControllerBase
{
    [HttpGet]
    [AllowAnyAuthenticatedUser]
    [ProducesResponseType<BannerImageList>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_ListBannerImages")]
    public async Task<IActionResult> List(CancellationToken cancellationToken) => Ok(await banners.ListImagesAsync(cancellationToken));

    /// <summary>The image itself. It changes only when uploaded again, so the browser keeps it for a day.</summary>
    [HttpGet("{imageId:guid}/content")]
    [AllowAnyAuthenticatedUser]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK, "image/webp")]
    [EndpointName("IdentityAccess_GetBannerImageContent")]
    public async Task<IActionResult> Content(Guid imageId, CancellationToken cancellationToken)
    {
        if (await banners.ReadContentAsync(imageId, cancellationToken) is not { } image)
        {
            return Failure(AdministrationError.NotFound);
        }

        Response.Headers.CacheControl = "private, max-age=86400";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(image.Content, image.ContentType);
    }

    /// <summary>Adds an image, or replaces the one with the same code: a WebP of at most 300 KB, with its captions, alt text, focal point and credit.</summary>
    [HttpPost]
    [RequirePermission(PermissionCatalogue.MasterDataManage)]
    [SensitiveWrite]
    [DisableFormValueModelBinding]
    [ProducesResponseType<BannerImageSummary>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_SaveBannerImage")]
    public async Task<IActionResult> Save(CancellationToken cancellationToken)
    {
        (IFormCollection? form, DocumentUploadForm.Refusal refusal) = await DocumentUploadForm.ReadAsync(HttpContext, 300 * 1024, cancellationToken);
        if (refusal != DocumentUploadForm.Refusal.None)
        {
            return Failure(refusal == DocumentUploadForm.Refusal.TooLarge ? AdministrationError.PayloadTooLarge : AdministrationError.UnsupportedMediaType);
        }

        (BannerImageUpload? upload, List<Errors.FieldError> errors) = await BannerImageForm.ReadAsync(form!, cancellationToken);
        return upload is null ? ValidationFailed(errors) : Respond(await banners.SaveImageAsync(CallerId, upload, cancellationToken));
    }
}

/// <summary>The signed-in person's own banner (review round 8, point 07): the green one, one image, or images that take turns.</summary>
[Route("api/v1/users/current/banner")]
[Tags("IdentityAccess")]
public sealed class CurrentUserBannerController(IBannerService banners) : AdministrationControllerBase
{
    /// <summary>The person's choice; the green banner when they never chose.</summary>
    [HttpGet]
    [AllowAnyAuthenticatedUser]
    [ProducesResponseType<BannerPreferenceDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_GetBannerPreference")]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) => Ok(await banners.GetPreferenceAsync(CallerId, cancellationToken));

    /// <summary>Saves the choice. One image takes exactly one image, the turns at least one (422 <c>IDENTITY_ACCESS_BANNER_IMAGES_INVALID</c>).</summary>
    [HttpPut]
    [AllowAnyAuthenticatedUser]
    [ProducesResponseType<BannerPreferenceDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_SetBannerPreference")]
    public async Task<IActionResult> Set(BannerPreferenceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate(out BannerStyle? style) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : Respond(await banners.SetPreferenceAsync(CallerId, style!.Value, request.ImageIds ?? [], cancellationToken));
    }
}

/// <summary>
/// The signed-in person's own quick actions in the welcome banner (review round 8, point 08): up to six, in their order, or their role's set until they
/// choose. Which actions exist, and who may do each, is the browser's catalogue; the server keeps the choice.
/// </summary>
[Route("api/v1/users/current/quick-actions")]
[Tags("IdentityAccess")]
public sealed class CurrentUserQuickActionsController(IBannerService banners) : AdministrationControllerBase
{
    /// <summary>The person's choice; <c>actions</c> null while they keep their role's set.</summary>
    [HttpGet]
    [AllowAnyAuthenticatedUser]
    [ProducesResponseType<QuickActionsDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_GetQuickActions")]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) => Ok(await banners.GetQuickActionsAsync(CallerId, cancellationToken));

    /// <summary>
    /// Saves the choice: at most six distinct codes (422 <c>IDENTITY_ACCESS_QUICK_ACTIONS_INVALID</c>), or <c>actions</c> null to restore the role's set.
    /// </summary>
    [HttpPut]
    [AllowAnyAuthenticatedUser]
    [ProducesResponseType<QuickActionsDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_SetQuickActions")]
    public async Task<IActionResult> Set(QuickActionsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Respond(await banners.SetQuickActionsAsync(CallerId, request.Actions, cancellationToken));
    }
}
