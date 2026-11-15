using System.Globalization;
using PMPlatform.Api.Errors;
using PMPlatform.Application.Features.IdentityAccess.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Api.Models.IdentityAccess;

/// <summary>A person's banner (review round 8, point 07): <c>style</c> DEFAULT, IMAGE or ROTATE, and the images chosen, in order.</summary>
public sealed record BannerPreferenceRequest(string? Style, IReadOnlyList<Guid>? ImageIds = null)
{
    internal List<FieldError> Validate(out BannerStyle? style)
    {
        List<FieldError> errors = [];
        if (string.IsNullOrEmpty(Style))
        {
            errors.Add(new FieldError("style", FieldError.Required));
        }

        style = QueryParameters.OneEnum<BannerStyle>(Style, "style", errors);
        if (ImageIds is { Count: > 50 })
        {
            errors.Add(new FieldError("imageIds", FieldError.MaxLength));
        }

        return errors;
    }
}

/// <summary>
/// An image upload's form (review round 8, point 07): the WebP in part <c>file</c>, and its words in the fields <c>code</c>, <c>captionAr</c>,
/// <c>captionEn</c>, <c>altAr</c>, <c>altEn</c>, <c>focalX</c>, <c>focalY</c> (0–100), <c>credit</c> (optional), <c>width</c>, <c>height</c> and
/// <c>sortOrder</c>.
/// </summary>
internal static class BannerImageForm
{
    public static async Task<(BannerImageUpload? Upload, List<FieldError> Errors)> ReadAsync(IFormCollection form, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        string? Text(string field, int maxLength, bool required = true)
        {
            string? value = form[field].FirstOrDefault()?.Trim();
            if (string.IsNullOrEmpty(value))
            {
                if (required)
                {
                    errors.Add(new FieldError(field, FieldError.Required));
                }

                return null;
            }

            if (value.Length > maxLength)
            {
                errors.Add(new FieldError(field, FieldError.MaxLength));
            }

            return value;
        }

        int Number(string field, int min, int max)
        {
            string? value = form[field].FirstOrDefault();
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) || number < min || number > max)
            {
                errors.Add(new FieldError(field, FieldError.OutOfRange));
            }

            return number;
        }

        string? code = Text("code", 64);
        string? captionAr = Text("captionAr", 200);
        string? captionEn = Text("captionEn", 200);
        string? altAr = Text("altAr", 500);
        string? altEn = Text("altEn", 500);
        string? credit = Text("credit", 200, required: false);
        int focalX = Number("focalX", 0, 100);
        int focalY = Number("focalY", 0, 100);
        int width = Number("width", 1, 10000);
        int height = Number("height", 1, 10000);
        int sortOrder = Number("sortOrder", 0, 10000);
        IFormFile? file = form.Files.GetFile("file");
        if (file is null || file.Length == 0)
        {
            errors.Add(new FieldError("file", FieldError.Required));
        }

        if (errors.Count > 0)
        {
            return (null, errors);
        }

        using MemoryStream content = new();
        await file!.CopyToAsync(content, cancellationToken).ConfigureAwait(false);
        return (new BannerImageUpload(
            code!, new BilingualLabel(captionAr!, captionEn!), new BilingualLabel(altAr!, altEn!), (short)focalX, (short)focalY, credit, width, height, sortOrder,
            file.ContentType, content.ToArray()), errors);
    }
}
