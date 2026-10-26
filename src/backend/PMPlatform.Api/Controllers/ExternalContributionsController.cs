using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.ExternalParticipation;
using PMPlatform.Api.Models.FinancialKpi;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.ExternalParticipation.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// WF-13 Path B (TASK-066): the entity's answers, as immutable revisions — SCR-162, SCR-164, SCR-165. The request's responder drafts and
/// submits; AHDA's assigned reviewer accepts, returns or rejects. No operation edits a submitted revision's values: a return opens the next
/// revision as a DRAFT for the responder to correct. An external caller gets the least-disclosure projection (R-20).
/// </summary>
[Route(Collection)]
[Tags("ExternalParticipation")]
[OmitMaskedFields]
public sealed class ExternalContributionsController(IExternalContributionService contributions) : AdministrationControllerBase
{
    private const string Collection = "api/v1/external-contributions";

    /// <summary>A request's revisions, newest first. <c>externalUpdateRequestId</c> is required (R-3).</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.ExternalRequestView)]
    [ProducesResponseType<ExternalContributionPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ExternalParticipation_ListExternalContributions")]
    public async Task<IActionResult> List([FromQuery] Guid? externalUpdateRequestId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(externalUpdateRequestId, "externalUpdateRequestId", errors);
        PageRequest paging = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0 ? ValidationFailed(errors) : Ok(await contributions.ListAsync(CallerId, externalUpdateRequestId!.Value, paging, cancellationToken));
    }

    [HttpGet("{externalContributionId:guid}")]
    [RequirePermission(PermissionCatalogue.ExternalRequestView)]
    [ProducesResponseType<ExternalContributionDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ExternalParticipation_GetExternalContribution")]
    public async Task<IActionResult> Get(Guid externalContributionId, CancellationToken cancellationToken) =>
        Respond(await contributions.GetAsync(CallerId, externalContributionId, cancellationToken));

    /// <summary>Drafts revision 1 of the answer to an ISSUED request, as its named responder. Values outside the request's typed schema are refused.</summary>
    [HttpPost]
    [RequirePermission(PermissionCatalogue.ExternalContributionRespond)]
    [SensitiveWrite]
    [ProducesResponseType<ExternalContributionDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("ExternalParticipation_CreateExternalContribution")]
    public async Task<IActionResult> Create(ExternalContributionCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate(out IReadOnlyList<ContributionFieldInput> fields) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : RespondCreated(await contributions.StartAsync(CallerId, request.ExternalUpdateRequestId!.Value, fields, cancellationToken), $"/{Collection}", c => c.Id);
    }

    /// <summary>A DRAFT revision's values, as a whole, by the responder. Requires <c>If-Match</c> (R-21). A submitted revision is never edited (409).</summary>
    [HttpPut("{externalContributionId:guid}")]
    [RequirePermission(PermissionCatalogue.ExternalContributionRespond)]
    [SensitiveWrite]
    [ProducesResponseType<ExternalContributionDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ExternalParticipation_UpdateExternalContribution")]
    public async Task<IActionResult> Update(Guid externalContributionId, ExternalContributionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return !TryReadIfMatch(required: true, out uint? version, out IActionResult? problem) ? problem!
            : request.Validate(out IReadOnlyList<ContributionFieldInput> fields) is { Count: > 0 } errors ? ValidationFailed(errors)
            : Respond(await contributions.UpdateAsync(CallerId, externalContributionId, fields, version!.Value, cancellationToken));
    }

    /// <summary>DRAFT → SUBMITTED, complete; from here its values and the source version it was answered against never change.</summary>
    [HttpPost("{externalContributionId:guid}/submit")]
    [RequirePermission(PermissionCatalogue.ExternalContributionRespond)]
    [SensitiveWrite]
    [ProducesResponseType<ExternalContributionDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ExternalParticipation_Submit")]
    public Task<IActionResult> Submit(Guid externalContributionId, CancellationToken cancellationToken) =>
        CommandAsync((version, ct) => contributions.SubmitAsync(CallerId, externalContributionId, version, ct), cancellationToken);

    /// <summary>SUBMITTED → UNDER_REVIEW, by the request's assigned reviewer. AHDA only.</summary>
    [HttpPost("{externalContributionId:guid}/start-review")]
    [RequirePermission(PermissionCatalogue.ExternalContributionReview)]
    [SensitiveWrite]
    [ProducesResponseType<ExternalContributionDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ExternalParticipation_StartReview")]
    public Task<IActionResult> StartReview(Guid externalContributionId, CancellationToken cancellationToken) =>
        CommandAsync((version, ct) => contributions.StartReviewAsync(CallerId, externalContributionId, version, ct), cancellationToken);

    /// <summary>
    /// UNDER_REVIEW → ACCEPTED_PENDING_APPLICATION, or APPLIED for a reference-only answer: fit to apply, not an approval (BR-EXT-014) and not
    /// an application (BR-EXT-015). The assigned reviewer, with an internal note if any.
    /// </summary>
    [HttpPost("{externalContributionId:guid}/accept")]
    [RequirePermission(PermissionCatalogue.ExternalContributionReview)]
    [SensitiveWrite]
    [ProducesResponseType<ExternalContributionDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ExternalParticipation_Accept")]
    public Task<IActionResult> Accept(Guid externalContributionId, ContributionAcceptCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate(out ContributionReviewDecision decision) is { Count: > 0 } errors
            ? Task.FromResult(ValidationFailed(errors) as IActionResult)
            : CommandAsync((version, ct) => contributions.AcceptAsync(CallerId, externalContributionId, decision, version, ct), cancellationToken);
    }

    /// <summary>UNDER_REVIEW → RETURNED, with the reason the entity reads; opens the next revision as a DRAFT holding the same values to correct.</summary>
    [HttpPost("{externalContributionId:guid}/return")]
    [RequirePermission(PermissionCatalogue.ExternalContributionReview)]
    [SensitiveWrite]
    [ProducesResponseType<ExternalContributionDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ExternalParticipation_Return")]
    public Task<IActionResult> Return(Guid externalContributionId, ContributionDecisionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate(out ContributionReviewDecision decision) is { Count: > 0 } errors
            ? Task.FromResult(ValidationFailed(errors) as IActionResult)
            : CommandAsync((version, ct) => contributions.ReturnAsync(CallerId, externalContributionId, decision, version, ct), cancellationToken);
    }

    /// <summary>UNDER_REVIEW → REJECTED, with the reason the entity reads; the request closes.</summary>
    [HttpPost("{externalContributionId:guid}/reject")]
    [RequirePermission(PermissionCatalogue.ExternalContributionReview)]
    [SensitiveWrite]
    [ProducesResponseType<ExternalContributionDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ExternalParticipation_Reject")]
    public Task<IActionResult> Reject(Guid externalContributionId, ContributionDecisionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate(out ContributionReviewDecision decision) is { Count: > 0 } errors
            ? Task.FromResult(ValidationFailed(errors) as IActionResult)
            : CommandAsync((version, ct) => contributions.RejectAsync(CallerId, externalContributionId, decision, version, ct), cancellationToken);
    }

    /// <summary>A command with the caller's version when they send one (R-21).</summary>
    private async Task<IActionResult> CommandAsync(
        Func<uint?, CancellationToken, Task<AdministrationResult<Versioned<ExternalContributionDetail>>>> command, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await command(version, cancellationToken))
            : problem!;
}
