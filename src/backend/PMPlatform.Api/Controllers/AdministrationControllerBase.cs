using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.DocumentManagement;
using PMPlatform.Application.Common.DataImport;
using PMPlatform.Application.Common.Validation;
using PMPlatform.Application.Features.DocumentManagement.Contracts;
using PMPlatform.Application.Features.Reports.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// What the FG-03 (TASK-031), FG-04 (TASK-034), WF-11 (TASK-035), WF-12 (TASK-037), WF-01 (TASK-041) and FG-02 (TASK-071) controllers share: the caller's id, the api-conventions
/// §4.4 answer to each refusal, and R-21 concurrency — an <c>ETag</c> on every single mutable resource, <c>If-Match</c> required on
/// <c>PUT</c> and honoured on a command when sent.
/// </summary>
[ApiController]
public abstract class AdministrationControllerBase : ControllerBase
{
    /// <summary>The <c>sub</c> of the validated access token; every administration endpoint requires one.</summary>
    protected Guid CallerId => Guid.Parse(User.FindFirstValue(SessionTokenClaims.Subject)!);

    private protected ObjectResult ValidationFailed(IReadOnlyList<FieldError> errors) =>
        ApiProblem.Result(HttpContext, StatusCodes.Status400BadRequest, ErrorCodes.ValidationFailed, "Validation failed.", errors);

    private protected IActionResult Respond<T>(AdministrationResult<T> result)
        where T : class =>
        result.Succeeded ? Ok(result.Value) : Failure(result.Error);

    private protected IActionResult Respond<T>(AdministrationResult<Versioned<T>> result)
        where T : class
    {
        if (!result.Succeeded)
        {
            return Failure(result.Error);
        }

        SetETag(result.Value.Version);
        return Ok(result.Value.Value);
    }

    /// <summary>201 with <c>Location</c> <paramref name="collectionPath"/>/{id} and the new resource's <c>ETag</c> (R-5).</summary>
    private protected IActionResult RespondCreated<T>(AdministrationResult<Versioned<T>> result, string collectionPath, Func<T, Guid> idOf)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(idOf);
        if (!result.Succeeded)
        {
            return Failure(result.Error);
        }

        SetETag(result.Value.Version);
        return Created($"{collectionPath}/{idOf(result.Value.Value)}", result.Value.Value);
    }

    /// <summary>
    /// The version <c>If-Match</c> names. Absent: 428 if <paramref name="required"/> (a <c>PUT</c>), else null. Anything
    /// but one strong ETag this API issued cannot match, so it is 412.
    /// </summary>
    private protected bool TryReadIfMatch(bool required, out uint? version, out IActionResult? problem)
    {
        version = null;
        problem = null;
        string? header = Request.Headers.IfMatch;
        if (string.IsNullOrEmpty(header))
        {
            if (required)
            {
                problem = ApiProblem.Result(HttpContext, StatusCodes.Status428PreconditionRequired, ErrorCodes.PreconditionRequired, "If-Match required.");
            }

            return !required;
        }

        if (header.Length > 2 && header[0] == '"' && header[^1] == '"'
            && uint.TryParse(header.AsSpan(1, header.Length - 2), NumberStyles.None, CultureInfo.InvariantCulture, out uint parsed))
        {
            version = parsed;
            return true;
        }

        problem = PreconditionFailed();
        return false;
    }

    /// <summary>
    /// R-8: a version's bytes as an attachment, never rendered by the browser, never cached (the content is classified).
    /// The response disposes the stream.
    /// </summary>
    private protected IActionResult Attachment(AdministrationResult<DocumentContent> result) =>
        result.Succeeded ? Attachment(result.Value.Content, result.Value.FileName) : Failure(result.Error);

    /// <summary>R-8 for a report job's output (TASK-071): authorised at download time by the module, then sent as a document's bytes are.</summary>
    private protected IActionResult Attachment(AdministrationResult<ReportOutputDownload> result) =>
        result.Succeeded ? Attachment(result.Value.Content, result.Value.FileName) : Failure(result.Error);

    /// <summary>TASK-102: the register's migration workbook template, as an attachment (R-8).</summary>
    private protected async Task<IActionResult> ImportTemplateAsync(IDataImportService imports, string registerCode, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(imports);
        DataImportResult<byte[]> template = await imports.TemplateAsync(CallerId, registerCode, cancellationToken);
        return template.Value is { } content
            ? Attachment(new MemoryStream(content), $"{registerCode.ToLowerInvariant()}-import-template.xlsx")
            : ImportRefused(template.Refusal);
    }

    /// <summary>
    /// TASK-102: the workbook in part <c>file</c> (R-8), validated, or imported with <paramref name="commit"/>. A report with issues is still 200:
    /// it is the answer asked for, and nothing was written.
    /// </summary>
    private protected async Task<IActionResult> ImportAsync(IDataImportService imports, string registerCode, bool commit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(imports);
        (IFormCollection? form, DocumentUploadForm.Refusal refusal) = await DocumentUploadForm.ReadAsync(HttpContext, ImportLimits.MaxFileBytes, cancellationToken);
        if (refusal != DocumentUploadForm.Refusal.None)
        {
            return Failure(refusal == DocumentUploadForm.Refusal.TooLarge ? AdministrationError.PayloadTooLarge : AdministrationError.UnsupportedMediaType);
        }

        IFormFile? file = form!.Files.GetFile(DocumentUploadForm.FilePart);
        string? fileIssue = file is null || file.Length == 0 ? FieldError.Required
            : TextInput.Violation(file.FileName) ?? (file.FileName.Length > 255 ? FieldError.MaxLength : null);
        if (fileIssue is not null)
        {
            return ValidationFailed([new FieldError(DocumentUploadForm.FilePart, fileIssue)]);
        }

        await using Stream content = file!.OpenReadStream();
        DataImportResult<ImportReport> result = await imports.ImportAsync(CallerId, registerCode, new ImportUpload(file.FileName, content), commit, cancellationToken);
        return result.Value is { } report ? Ok(report) : ImportRefused(result.Refusal);
    }

    private IActionResult ImportRefused(DataImportRefusal refusal) => refusal switch
    {
        DataImportRefusal.NotFound => Failure(AdministrationError.NotFound),
        DataImportRefusal.Forbidden => Failure(AdministrationError.Forbidden),
        DataImportRefusal.NotWorkbook => Failure(AdministrationError.UnsupportedMediaType),
        DataImportRefusal.TooManyRows => Failure(AdministrationError.PayloadTooLarge),
        DataImportRefusal.None or _ => throw new InvalidOperationException($"Not an import refusal: {refusal}."),
    };

    private FileStreamResult Attachment(Stream content, string fileName)
    {
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.CacheControl = "no-store";
        return File(content, "application/octet-stream", fileName);
    }

    private protected IActionResult Failure(AdministrationError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        List<FieldError> fields = [.. error.Fields.Select(f => new FieldError(f.Field, f.Code))];
        return error.Kind switch
        {
            AdministrationErrorKind.NotFound => ApiProblem.Result(HttpContext, StatusCodes.Status404NotFound, ErrorCodes.NotFound, "Not found."),
            AdministrationErrorKind.Forbidden => ApiProblem.Result(HttpContext, StatusCodes.Status403Forbidden, ErrorCodes.PermissionDenied, "Permission denied."),
            AdministrationErrorKind.RuleViolated => ApiProblem.Result(HttpContext, StatusCodes.Status422UnprocessableEntity, error.Code!, "Business rule violated.", fields),
            AdministrationErrorKind.Conflict => ApiProblem.Result(HttpContext, StatusCodes.Status409Conflict, error.Code!, "Conflict.", fields),
            AdministrationErrorKind.InvalidTransition => ApiProblem.Result(HttpContext, StatusCodes.Status409Conflict, ErrorCodes.InvalidTransition, "Invalid transition."),
            AdministrationErrorKind.TerminalState => ApiProblem.Result(HttpContext, StatusCodes.Status409Conflict, ErrorCodes.TerminalState, "Terminal state."),
            AdministrationErrorKind.PreconditionFailed => PreconditionFailed(),
            AdministrationErrorKind.Unavailable => ApiProblem.Result(HttpContext, StatusCodes.Status503ServiceUnavailable, ErrorCodes.Unavailable, "Unavailable."),
            AdministrationErrorKind.PayloadTooLarge => ApiProblem.Result(HttpContext, StatusCodes.Status413PayloadTooLarge, ErrorCodes.PayloadTooLarge, "Payload too large."),
            AdministrationErrorKind.UnsupportedMediaType => ApiProblem.Result(HttpContext, StatusCodes.Status415UnsupportedMediaType, ErrorCodes.UnsupportedMediaType, "Unsupported media type."),
            _ => throw new InvalidOperationException($"Unknown administration error {error.Kind}."),
        };
    }

    private ObjectResult PreconditionFailed() =>
        ApiProblem.Result(HttpContext, StatusCodes.Status412PreconditionFailed, ErrorCodes.PreconditionFailed, "Precondition failed.");

    private void SetETag(uint version) => Response.Headers.ETag = string.Create(CultureInfo.InvariantCulture, $"\"{version}\"");
}
