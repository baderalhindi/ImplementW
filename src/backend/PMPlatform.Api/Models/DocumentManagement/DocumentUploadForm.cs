using Microsoft.AspNetCore.Http.Features;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Api.Models.MasterDataConfig;
using PMPlatform.Application.Features.DocumentManagement.Contracts;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Models.DocumentManagement;

/// <summary>
/// R-8: an upload is <c>multipart/form-data</c> with the file in part <c>file</c>. MOD-050 adds the metadata as form fields
/// named as the JSON properties are (<c>title.text</c>, <c>title.language</c>, …). The request may be as large as the
/// upload policy allows and no larger: the limit is set before the body is read.
/// </summary>
internal static class DocumentUploadForm
{
    public const string FilePart = "file";

    /// <summary>Room for the multipart boundaries and the metadata fields around the file.</summary>
    private const long EnvelopeBytes = 1024 * 1024;

    public enum Refusal
    {
        None = 0,
        NotMultipart = 1,
        TooLarge = 2,
    }

    public static async Task<(IFormCollection? Form, Refusal Refusal)> ReadAsync(HttpContext context, DocumentUploadPolicy policy, CancellationToken cancellationToken)
    {
        HttpRequest request = context.Request;
        if (!request.HasFormContentType || request.ContentType?.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase) != true)
        {
            return (null, Refusal.NotMultipart);
        }

        long limit = policy.MaxFileSizeBytes + EnvelopeBytes;
        if (request.ContentLength > limit)
        {
            return (null, Refusal.TooLarge);
        }

        if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } bodySize)
        {
            bodySize.MaxRequestBodySize = limit;
        }

        context.Features.Set<IFormFeature>(new FormFeature(request, new FormOptions { MultipartBodyLengthLimit = limit }));
        try
        {
            return (await request.ReadFormAsync(cancellationToken).ConfigureAwait(false), Refusal.None);
        }
        catch (InvalidDataException)
        {
            return (null, Refusal.TooLarge);
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return (null, Refusal.TooLarge);
        }
    }

    /// <summary>The file part, required and not empty. Its size and media type are the upload policy's to judge.</summary>
    public static DocumentFile? File(IFormCollection form, List<FieldError> errors)
    {
        IFormFile? file = form.Files.GetFile(FilePart);
        if (file is null || file.Length == 0)
        {
            errors.Add(new FieldError(FilePart, FieldError.Required));
            return null;
        }

        return new DocumentFile(file.FileName, file.ContentType ?? string.Empty, file.Length, file.OpenReadStream());
    }

    /// <summary>MOD-050's metadata fields.</summary>
    public static DocumentDraft? Draft(IFormCollection form, List<FieldError> errors)
    {
        int before = errors.Count;
        NarrativeText? title = new NarrativeTextRequest(Value(form, "title.text"), Value(form, "title.language")).Validate("title", errors);
        string? descriptionText = Value(form, "description.text");
        string? descriptionLanguage = Value(form, "description.language");
        NarrativeText? description = descriptionText is null && descriptionLanguage is null
            ? null
            : new NarrativeTextRequest(descriptionText, descriptionLanguage).Validate("description", errors);
        Guid? documentType = Id(form, "documentTypeItemId", required: true, errors);
        Guid? classification = Id(form, "dataClassificationItemId", required: true, errors);
        Guid? project = Id(form, "projectId", required: false, errors);
        return errors.Count == before ? new DocumentDraft(title!, description, documentType!.Value, classification!.Value, project) : null;
    }

    private static string? Value(IFormCollection form, string key) =>
        form.TryGetValue(key, out Microsoft.Extensions.Primitives.StringValues values) && values.Count > 0 ? values[0] : null;

    private static Guid? Id(IFormCollection form, string key, bool required, List<FieldError> errors)
    {
        string? value = Value(form, key);
        if (value is null)
        {
            if (required)
            {
                RequestValidation.RequireId(null, key, errors);
            }

            return null;
        }

        if (Guid.TryParse(value, out Guid id) && id != Guid.Empty)
        {
            return id;
        }

        errors.Add(new FieldError(key, FieldError.Malformed));
        return null;
    }
}
