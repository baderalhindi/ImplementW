using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Models.FinancialKpi;
using PMPlatform.Api.Models.Progress;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.DocumentManagement.Contracts;
using PMPlatform.Application.Features.FinancialKpi.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// WF-14 Financial Progress (TASK-052): the Approved Budget's versions in SAR, project total only (ADR-008). A version is opened
/// DRAFT, given its referenced document, and submitted to WF-11; approval makes it ACTIVE — the budget of record — and supersedes
/// the previous one. Its review history is
/// <c>GET /approval-instances?subjectModule=FinancialKpi&amp;subjectType=FinancialCommitment&amp;subjectId={id}</c>.
/// </summary>
[Route(Collection)]
[Tags("FinancialKpi")]
[OmitMaskedFields]
public sealed class FinancialCommitmentsController(IFinancialCommitmentService commitments) : AdministrationControllerBase
{
    private const string Collection = "api/v1/financial-commitments";

    /// <summary>The project's versions, newest first; <c>isCurrent</c> marks the ACTIVE one.</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.FinancialView)]
    [ProducesResponseType<FinancialCommitmentPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("FinancialKpi_ListFinancialCommitments")]
    public async Task<IActionResult> List([FromQuery] Guid? projectId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken) =>
        ProjectCollectionQuery.Validate(projectId, page, pageSize, out PageRequest paging) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : Ok(await commitments.ListAsync(CallerId, projectId!.Value, paging, cancellationToken));

    [HttpGet("{commitmentId:guid}")]
    [RequirePermission(PermissionCatalogue.FinancialView)]
    [ProducesResponseType<FinancialCommitmentDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("FinancialKpi_GetFinancialCommitment")]
    public async Task<IActionResult> Get(Guid commitmentId, CancellationToken cancellationToken) =>
        Respond(await commitments.GetAsync(CallerId, commitmentId, cancellationToken));

    /// <summary>Opens the project's next Approved Budget version as a DRAFT.</summary>
    [HttpPost]
    [RequirePermission(PermissionCatalogue.FinancialSubmit)]
    [SensitiveWrite]
    [ProducesResponseType<FinancialCommitmentDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("FinancialKpi_CreateFinancialCommitment")]
    public async Task<IActionResult> Create(FinancialCommitmentCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate(out FinancialCommitmentDraft? draft) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : RespondCreated(await commitments.CreateAsync(CallerId, draft!, cancellationToken), $"/{Collection}", c => c.Id);
    }

    /// <summary>A DRAFT or RETURNED version's figures, as a whole. Requires <c>If-Match</c> (R-21).</summary>
    [HttpPut("{commitmentId:guid}")]
    [RequirePermission(PermissionCatalogue.FinancialSubmit)]
    [SensitiveWrite]
    [ProducesResponseType<FinancialCommitmentDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("FinancialKpi_UpdateFinancialCommitment")]
    public async Task<IActionResult> Update(Guid commitmentId, FinancialCommitmentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return !TryReadIfMatch(required: true, out uint? version, out IActionResult? problem) ? problem!
            : request.Validate(out FinancialCommitmentChanges? changes) is { Count: > 0 } errors ? ValidationFailed(errors)
            : Respond(await commitments.UpdateAsync(CallerId, commitmentId, changes!, version!.Value, cancellationToken));
    }

    /// <summary>HARD_DRAFT. R-40: a version that is not there is 204 as well.</summary>
    [HttpDelete("{commitmentId:guid}")]
    [RequirePermission(PermissionCatalogue.FinancialSubmit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [EndpointName("FinancialKpi_DeleteFinancialCommitment")]
    public async Task<IActionResult> Delete(Guid commitmentId, CancellationToken cancellationToken)
    {
        if (!TryReadIfMatch(required: false, out uint? version, out IActionResult? problem))
        {
            return problem!;
        }

        AdministrationError? error = await commitments.DeleteAsync(CallerId, commitmentId, version, cancellationToken);
        return error is null || error.Kind == AdministrationErrorKind.NotFound ? NoContent() : Failure(error);
    }

    /// <summary>DRAFT or RETURNED → SUBMITTED to WF-11, once a referenced document is held (422 <c>FINANCIAL_BUDGET_DOCUMENT_REQUIRED</c>).</summary>
    [HttpPost("{commitmentId:guid}/submit")]
    [RequirePermission(PermissionCatalogue.FinancialSubmit)]
    [SensitiveWrite]
    [ProducesResponseType<FinancialCommitmentDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("FinancialKpi_SubmitFinancialCommitment")]
    public async Task<IActionResult> Submit(Guid commitmentId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await commitments.SubmitAsync(CallerId, commitmentId, version, cancellationToken))
            : problem!;

    /// <summary>The version's referenced documents and the evidence types it holds CLEAN evidence for.</summary>
    [HttpGet("{commitmentId:guid}/documents")]
    [RequirePermission(PermissionCatalogue.FinancialView)]
    [ProducesResponseType<CommitmentDocumentDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("FinancialKpi_GetFinancialCommitmentDocuments")]
    public async Task<IActionResult> GetDocuments(Guid commitmentId, CancellationToken cancellationToken) =>
        Respond(await commitments.GetDocumentsAsync(CallerId, commitmentId, cancellationToken));

    /// <summary>
    /// ADR-003 §8.2 edge 38: links a document to a DRAFT or RETURNED version and pins one of its CLEAN versions as the reference.
    /// A version not CLEAN is 409 <c>DOCUMENT_NOT_AVAILABLE</c>, and nothing is linked.
    /// </summary>
    [HttpPost("{commitmentId:guid}/documents")]
    [RequirePermission(PermissionCatalogue.FinancialSubmit)]
    [SensitiveWrite]
    [ProducesResponseType<EvidenceReferenceDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("FinancialKpi_AttachFinancialCommitmentDocument")]
    public async Task<IActionResult> AttachDocument(Guid commitmentId, CommitmentDocumentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Validate(out CommitmentDocumentAttachment? attachment) is { Count: > 0 } errors)
        {
            return ValidationFailed(errors);
        }

        AdministrationResult<EvidenceReferenceDetail> result = await commitments.AttachDocumentAsync(CallerId, commitmentId, attachment!, cancellationToken);
        return result.Succeeded ? Created($"/api/v1/evidence-references/{result.Value.Id}", result.Value) : Failure(result.Error);
    }

    /// <summary>Withdraws a DRAFT or RETURNED version's referenced document; the document and its history remain.</summary>
    [HttpPost("{commitmentId:guid}/documents/{evidenceReferenceId:guid}/withdraw")]
    [RequirePermission(PermissionCatalogue.FinancialSubmit)]
    [SensitiveWrite]
    [ProducesResponseType<EvidenceReferenceDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("FinancialKpi_WithdrawFinancialCommitmentDocument")]
    public async Task<IActionResult> WithdrawDocument(Guid commitmentId, Guid evidenceReferenceId, CancellationToken cancellationToken) =>
        Respond(await commitments.WithdrawDocumentAsync(CallerId, commitmentId, evidenceReferenceId, cancellationToken));
}
