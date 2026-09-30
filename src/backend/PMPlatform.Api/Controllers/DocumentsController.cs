using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.DocumentManagement;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.DocumentManagement.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.DocumentManagement;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// WF-12 documents (TASK-037): SCR-120–124 and MOD-050–053. Upload is <c>multipart/form-data</c>; content is served as an
/// attachment only from a CLEAN version (R-8). Nothing is ever deleted: a document is archived, a link ended, evidence
/// withdrawn. Each call is decided on the document's own anchors, not on project membership (CTL-20).
/// </summary>
[Route(Collection)]
[Tags("DocumentManagement")]
public sealed class DocumentsController(IDocumentService documents, DocumentUploadPolicy uploadPolicy) : AdministrationControllerBase
{
    private const string Collection = "api/v1/documents";
    private const int QueryTextLength = 200;

    /// <summary>
    /// SCR-120 Library, SCR-121 (<c>projectId</c>), SCR-122 Recent: only documents the caller may read. Filters: <c>projectId</c>,
    /// <c>status</c> (a set), <c>q</c> (title). Most recently changed first.
    /// </summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.DocumentView)]
    [ProducesResponseType<DocumentPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("DocumentManagement_ListDocuments")]
    public async Task<IActionResult> List(
        [FromQuery] Guid? projectId, [FromQuery] string? status, [FromQuery] string? q, [FromQuery] int? page, [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        RequestValidation.Optional(q, "q", QueryTextLength, errors);
        DocumentQuery query = new(projectId, QueryParameters.EnumSet<DocumentStatus>(status, "status", errors), q, QueryParameters.Page(page, pageSize, errors));
        return errors.Count > 0 ? ValidationFailed(errors) : Ok(await documents.ListAsync(CallerId, query, cancellationToken));
    }

    /// <summary>SCR-123, with the latest version.</summary>
    [HttpGet("{documentId:guid}")]
    [RequirePermission(PermissionCatalogue.DocumentView)]
    [ProducesResponseType<DocumentDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("DocumentManagement_GetDocument")]
    public async Task<IActionResult> Get(Guid documentId, CancellationToken cancellationToken) =>
        Respond(await documents.GetAsync(CallerId, documentId, cancellationToken));

    /// <summary>MOD-050: the file in part <c>file</c>, the metadata as fields. Version 1 is SCAN_PENDING until scanned.</summary>
    [HttpPost]
    [RequirePermission(PermissionCatalogue.DocumentUpload)]
    [SensitiveWrite]
    [DisableFormValueModelBinding]
    [ProducesResponseType<DocumentDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("DocumentManagement_CreateDocument")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        (IFormCollection? form, IActionResult? problem) = await ReadUploadAsync(cancellationToken);
        if (form is null)
        {
            return problem!;
        }

        List<FieldError> errors = [];
        DocumentDraft? draft = DocumentUploadForm.Draft(form, errors);
        DocumentFile? file = DocumentUploadForm.File(form, errors);
        if (errors.Count > 0)
        {
            return ValidationFailed(errors);
        }

        await using Stream content = file!.Content;
        return RespondCreated(await documents.CreateAsync(CallerId, draft!, file, cancellationToken), $"/{Collection}", d => d.Id);
    }

    /// <summary>MOD-051. Requires <c>If-Match</c> (R-21). The project and owner never change.</summary>
    [HttpPut("{documentId:guid}")]
    [RequirePermission(PermissionCatalogue.DocumentManage)]
    [SensitiveWrite]
    [ProducesResponseType<DocumentDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("DocumentManagement_UpdateDocument")]
    public async Task<IActionResult> Update(Guid documentId, DocumentUpdateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return !TryReadIfMatch(required: true, out uint? version, out IActionResult? problem) ? problem!
            : request.Validate(out DocumentChanges? changes) is { Count: > 0 } errors ? ValidationFailed(errors)
            : Respond(await documents.UpdateAsync(CallerId, documentId, changes!, version!.Value, cancellationToken));
    }

    /// <summary>MOD-053: ACTIVE → ARCHIVED. Versions, links and evidence remain readable.</summary>
    [HttpPost("{documentId:guid}/archive")]
    [RequirePermission(PermissionCatalogue.DocumentManage)]
    [SensitiveWrite]
    [ProducesResponseType<DocumentDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("DocumentManagement_ArchiveDocument")]
    public async Task<IActionResult> Archive(Guid documentId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await documents.ArchiveAsync(CallerId, documentId, version, cancellationToken))
            : problem!;

    /// <summary>SCR-124: every version, newest first, each with its uploader, upload time and scan state.</summary>
    [HttpGet("{documentId:guid}/versions")]
    [RequirePermission(PermissionCatalogue.DocumentView)]
    [ProducesResponseType<IReadOnlyList<DocumentVersionDetail>>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("DocumentManagement_ListDocumentVersions")]
    public async Task<IActionResult> ListVersions(Guid documentId, CancellationToken cancellationToken) =>
        Respond(await documents.ListVersionsAsync(CallerId, documentId, cancellationToken));

    /// <summary>MOD-052 Replace Version: the file in part <c>file</c>. Earlier versions, and evidence pinned to them, do not change.</summary>
    [HttpPost("{documentId:guid}/versions")]
    [RequirePermission(PermissionCatalogue.DocumentUpload)]
    [SensitiveWrite]
    [DisableFormValueModelBinding]
    [ProducesResponseType<DocumentVersionDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("DocumentManagement_AddDocumentVersion")]
    public async Task<IActionResult> AddVersion(Guid documentId, CancellationToken cancellationToken)
    {
        (IFormCollection? form, IActionResult? problem) = await ReadUploadAsync(cancellationToken);
        if (form is null)
        {
            return problem!;
        }

        List<FieldError> errors = [];
        DocumentFile? file = DocumentUploadForm.File(form, errors);
        if (errors.Count > 0)
        {
            return ValidationFailed(errors);
        }

        await using Stream content = file!.Content;
        AdministrationResult<DocumentVersionDetail> result = await documents.AddVersionAsync(CallerId, documentId, file, cancellationToken);
        return result.Succeeded ? Created($"/{Collection}/{documentId}/versions/{result.Value.Id}", result.Value) : Failure(result.Error);
    }

    [HttpGet("{documentId:guid}/versions/{versionId:guid}")]
    [RequirePermission(PermissionCatalogue.DocumentView)]
    [ProducesResponseType<DocumentVersionDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("DocumentManagement_GetDocumentVersion")]
    public async Task<IActionResult> GetVersion(Guid documentId, Guid versionId, CancellationToken cancellationToken) =>
        Respond(await documents.GetVersionAsync(CallerId, documentId, versionId, cancellationToken));

    /// <summary>The version's bytes, only while CLEAN; otherwise 409 <c>DOCUMENT_NOT_AVAILABLE</c> (R-8).</summary>
    [HttpGet("{documentId:guid}/versions/{versionId:guid}/content")]
    [RequirePermission(PermissionCatalogue.DocumentView)]
    [ProducesResponseType<FileStreamResult>(StatusCodes.Status200OK, "application/octet-stream")]
    [EndpointName("DocumentManagement_DownloadDocumentVersion")]
    public async Task<IActionResult> Download(Guid documentId, Guid versionId, CancellationToken cancellationToken) =>
        Attachment(await documents.OpenContentAsync(CallerId, documentId, versionId, cancellationToken));

    /// <summary>SCAN_FAILED → SCAN_PENDING.</summary>
    [HttpPost("{documentId:guid}/versions/{versionId:guid}/rescan")]
    [RequirePermission(PermissionCatalogue.DocumentManage)]
    [SensitiveWrite]
    [ProducesResponseType<DocumentVersionDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("DocumentManagement_RescanDocumentVersion")]
    public async Task<IActionResult> Rescan(Guid documentId, Guid versionId, CancellationToken cancellationToken) =>
        Respond(await documents.RescanAsync(CallerId, documentId, versionId, cancellationToken));

    /// <summary>Every link, ended ones included, with the evidence pinned on each: nothing unlinked is lost.</summary>
    [HttpGet("{documentId:guid}/links")]
    [RequirePermission(PermissionCatalogue.DocumentView)]
    [ProducesResponseType<IReadOnlyList<BusinessLinkDetail>>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("DocumentManagement_ListDocumentLinks")]
    public async Task<IActionResult> ListLinks(Guid documentId, CancellationToken cancellationToken) =>
        Respond(await documents.ListLinksAsync(CallerId, documentId, cancellationToken));

    private async Task<(IFormCollection? Form, IActionResult? Problem)> ReadUploadAsync(CancellationToken cancellationToken)
    {
        (IFormCollection? form, DocumentUploadForm.Refusal refusal) = await DocumentUploadForm.ReadAsync(HttpContext, uploadPolicy, cancellationToken);
        return refusal switch
        {
            DocumentUploadForm.Refusal.None => (form, null),
            DocumentUploadForm.Refusal.NotMultipart => (null, Failure(AdministrationError.UnsupportedMediaType)),
            DocumentUploadForm.Refusal.TooLarge => (null, Failure(AdministrationError.PayloadTooLarge)),
            _ => throw new InvalidOperationException("Unknown upload refusal."),
        };
    }
}
