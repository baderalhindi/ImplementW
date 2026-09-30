using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.DocumentManagement.Contracts;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// The evidence path (TASK-037): an evidence reference and the content of the version it pins. A version that is not
/// CLEAN — SCAN_PENDING, SCAN_FAILED or QUARANTINED — is 409 <c>DOCUMENT_NOT_AVAILABLE</c> (R-8). Evidence is designated
/// by the module that owns the evidenced record, in process (<c>IDocumentLinks</c>), never through this API.
/// </summary>
[Route(Collection)]
[Tags("DocumentManagement")]
public sealed class EvidenceReferencesController(IDocumentService documents) : AdministrationControllerBase
{
    private const string Collection = "api/v1/evidence-references";

    [HttpGet("{evidenceReferenceId:guid}")]
    [RequirePermission(PermissionCatalogue.DocumentView)]
    [ProducesResponseType<EvidenceReferenceDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("DocumentManagement_GetEvidenceReference")]
    public async Task<IActionResult> Get(Guid evidenceReferenceId, CancellationToken cancellationToken) =>
        Respond(await documents.GetEvidenceAsync(CallerId, evidenceReferenceId, cancellationToken));

    [HttpGet("{evidenceReferenceId:guid}/content")]
    [RequirePermission(PermissionCatalogue.DocumentView)]
    [ProducesResponseType<FileStreamResult>(StatusCodes.Status200OK, "application/octet-stream")]
    [EndpointName("DocumentManagement_DownloadEvidence")]
    public async Task<IActionResult> Download(Guid evidenceReferenceId, CancellationToken cancellationToken) =>
        Attachment(await documents.OpenEvidenceContentAsync(CallerId, evidenceReferenceId, cancellationToken));
}
