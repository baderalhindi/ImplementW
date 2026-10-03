using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Api.Models.Milestone;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.DocumentManagement.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Milestone.Contracts;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// WF-05 (TASK-050): achievement claims of the shared milestones, one revision each — Draft, Submitted, Returned, Accepted, and
/// Superseded once a later revision is accepted. A correction is a new revision, never an edit. Acceptance is decided through
/// WF-11 (<c>/approval-tasks</c>) and applied by WF-05; a revision's review history is
/// <c>GET /approval-instances?subjectModule=Milestone&amp;subjectType=MilestoneAchievement&amp;subjectId={id}</c>.
/// </summary>
[Route(Collection)]
[Tags("Milestone")]
public sealed class MilestoneAchievementsController(IMilestoneAchievementService achievements) : AdministrationControllerBase
{
    private const string Collection = "api/v1/milestone-achievements";

    /// <summary>The revisions of one milestone (<c>projectMilestoneId</c>) or of a project's milestones (<c>projectId</c>), exactly one of the two, newest first.</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.MilestoneView)]
    [ProducesResponseType<MilestoneAchievementPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Milestone_ListMilestoneAchievements")]
    public async Task<IActionResult> List(
        [FromQuery] Guid? projectMilestoneId, [FromQuery] Guid? projectId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        if ((projectMilestoneId is null) == (projectId is null))
        {
            errors.Add(new FieldError(projectId is null ? "projectMilestoneId" : "projectId", projectId is null ? FieldError.Required : FieldError.NotAllowed));
        }

        PageRequest paging = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0
            ? ValidationFailed(errors)
            : Ok(await achievements.ListAsync(CallerId, new MilestoneAchievementQuery(projectMilestoneId, projectId), paging, cancellationToken));
    }

    [HttpGet("{achievementId:guid}")]
    [RequirePermission(PermissionCatalogue.MilestoneView)]
    [ProducesResponseType<MilestoneAchievementDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Milestone_GetMilestoneAchievement")]
    public async Task<IActionResult> Get(Guid achievementId, CancellationToken cancellationToken) =>
        Respond(await achievements.GetAsync(CallerId, achievementId, cancellationToken));

    /// <summary>Opens the milestone's next revision as a DRAFT: a first claim, a claim after a return, or a correction of an accepted one.</summary>
    [HttpPost]
    [RequirePermission(PermissionCatalogue.MilestoneSubmit)]
    [SensitiveWrite]
    [ProducesResponseType<MilestoneAchievementDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("Milestone_CreateMilestoneAchievement")]
    public async Task<IActionResult> Create(MilestoneAchievementCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate(out MilestoneAchievementDraft? draft) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : RespondCreated(await achievements.CreateAsync(CallerId, draft!, cancellationToken), $"/{Collection}", a => a.Id);
    }

    /// <summary>A DRAFT's claim, as a whole. Requires <c>If-Match</c> (R-21).</summary>
    [HttpPut("{achievementId:guid}")]
    [RequirePermission(PermissionCatalogue.MilestoneSubmit)]
    [SensitiveWrite]
    [ProducesResponseType<MilestoneAchievementDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Milestone_UpdateMilestoneAchievement")]
    public async Task<IActionResult> Update(Guid achievementId, MilestoneAchievementRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return !TryReadIfMatch(required: true, out uint? version, out IActionResult? problem) ? problem!
            : request.Validate(out MilestoneAchievementChanges? changes) is { Count: > 0 } errors ? ValidationFailed(errors)
            : Respond(await achievements.UpdateAsync(CallerId, achievementId, changes!, version!.Value, cancellationToken));
    }

    /// <summary>HARD_DRAFT. R-40: a revision that is not there — deleted already, or never the caller's to see — is 204 as well.</summary>
    [HttpDelete("{achievementId:guid}")]
    [RequirePermission(PermissionCatalogue.MilestoneSubmit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [EndpointName("Milestone_DeleteMilestoneAchievement")]
    public async Task<IActionResult> Delete(Guid achievementId, CancellationToken cancellationToken)
    {
        if (!TryReadIfMatch(required: false, out uint? version, out IActionResult? problem))
        {
            return problem!;
        }

        AdministrationError? error = await achievements.DeleteAsync(CallerId, achievementId, version, cancellationToken);
        return error is null || error.Kind == AdministrationErrorKind.NotFound ? NoContent() : Failure(error);
    }

    /// <summary>DRAFT → SUBMITTED to WF-11, once the evidence the policy makes mandatory is held. From here only WF-11's outcome changes it.</summary>
    [HttpPost("{achievementId:guid}/submit")]
    [RequirePermission(PermissionCatalogue.MilestoneSubmit)]
    [SensitiveWrite]
    [ProducesResponseType<MilestoneAchievementDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Milestone_SubmitMilestoneAchievement")]
    public async Task<IActionResult> Submit(Guid achievementId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await achievements.SubmitAsync(CallerId, achievementId, version, cancellationToken))
            : problem!;

    /// <summary>The revision's evidence links, and the evidence types EVIDENCE_POLICY makes mandatory against those it holds (PTBC-006).</summary>
    [HttpGet("{achievementId:guid}/evidence")]
    [RequirePermission(PermissionCatalogue.MilestoneView)]
    [ProducesResponseType<MilestoneEvidenceDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Milestone_GetMilestoneAchievementEvidence")]
    public async Task<IActionResult> GetEvidence(Guid achievementId, CancellationToken cancellationToken) =>
        Respond(await achievements.GetEvidenceAsync(CallerId, achievementId, cancellationToken));

    /// <summary>
    /// MOD-054 Attach (ADR-003 §8.2 edge 16): links a document to a DRAFT and pins one of its CLEAN versions as evidence of a type.
    /// A version not CLEAN is 409 <c>DOCUMENT_NOT_AVAILABLE</c>, and nothing is linked.
    /// </summary>
    [HttpPost("{achievementId:guid}/evidence")]
    [RequirePermission(PermissionCatalogue.MilestoneSubmit)]
    [SensitiveWrite]
    [ProducesResponseType<EvidenceReferenceDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("Milestone_AttachMilestoneAchievementEvidence")]
    public async Task<IActionResult> AttachEvidence(Guid achievementId, MilestoneEvidenceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Validate(out MilestoneEvidenceAttachment? attachment) is { Count: > 0 } errors)
        {
            return ValidationFailed(errors);
        }

        AdministrationResult<EvidenceReferenceDetail> result = await achievements.AttachEvidenceAsync(CallerId, achievementId, attachment!, cancellationToken);
        return result.Succeeded ? Created($"/api/v1/evidence-references/{result.Value.Id}", result.Value) : Failure(result.Error);
    }

    /// <summary>Withdraws a piece of a DRAFT's evidence; the document and its history remain.</summary>
    [HttpPost("{achievementId:guid}/evidence/{evidenceReferenceId:guid}/withdraw")]
    [RequirePermission(PermissionCatalogue.MilestoneSubmit)]
    [SensitiveWrite]
    [ProducesResponseType<EvidenceReferenceDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Milestone_WithdrawMilestoneAchievementEvidence")]
    public async Task<IActionResult> WithdrawEvidence(Guid achievementId, Guid evidenceReferenceId, CancellationToken cancellationToken) =>
        Respond(await achievements.WithdrawEvidenceAsync(CallerId, achievementId, evidenceReferenceId, cancellationToken));
}
