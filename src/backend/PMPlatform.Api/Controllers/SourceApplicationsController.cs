using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.ExternalParticipation;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.ExternalParticipation.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// WF-13 §7 (TASK-066): applying accepted revisions to their source records through the typed adapters — SCR-166. Each attempt is
/// recorded with its outcome: APPLIED, CONFLICT (the source changed since the answer; nothing applied until revalidated) or FAILED (the
/// source's own rules refused it). A retry with the same <c>Idempotency-Key</c> answers with the attempt it made and applies nothing again.
/// AHDA only.
/// </summary>
[Route(Collection)]
[Tags("ExternalParticipation")]
public sealed class SourceApplicationsController(ISourceApplicationService applications) : AdministrationControllerBase
{
    public const string ReplayedHeader = "Idempotent-Replayed";

    private const string Collection = "api/v1/source-applications";

    /// <summary>A revision's attempts, newest first. <c>externalContributionId</c> is required (R-3). Empty for an external caller.</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.ExternalRequestView)]
    [ProducesResponseType<SourceApplicationPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ExternalParticipation_ListSourceApplications")]
    public async Task<IActionResult> List([FromQuery] Guid? externalContributionId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(externalContributionId, "externalContributionId", errors);
        PageRequest paging = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0 ? ValidationFailed(errors) : Ok(await applications.ListAsync(CallerId, externalContributionId!.Value, paging, cancellationToken));
    }

    [HttpGet("{sourceApplicationId:guid}")]
    [RequirePermission(PermissionCatalogue.ExternalRequestView)]
    [ProducesResponseType<SourceApplicationDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ExternalParticipation_GetSourceApplication")]
    public async Task<IActionResult> Get(Guid sourceApplicationId, CancellationToken cancellationToken) =>
        Respond(await applications.GetAsync(CallerId, sourceApplicationId, cancellationToken));

    /// <summary>
    /// Attempts to apply an ACCEPTED_PENDING_APPLICATION revision to its source record: 201 with the attempt, whatever its outcome. After a
    /// CONFLICT the next attempt waits for a revalidation (409). A retry with the same key and body answers 201 with the same attempt and
    /// <c>Idempotent-Replayed: true</c>.
    /// </summary>
    [HttpPost]
    [RequirePermission(PermissionCatalogue.ExternalContributionApply)]
    [SensitiveWrite]
    [ProducesResponseType<SourceApplicationDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("ExternalParticipation_ApplyExternalContribution")]
    public async Task<IActionResult> Apply(SourceApplicationCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Validate() is { Count: > 0 } errors)
        {
            return ValidationFailed(errors);
        }

        // [SensitiveWrite] has already refused a request without a uuid key (R-36).
        Guid requestKey = Guid.Parse(Request.Headers[SensitiveWriteAttribute.HeaderName].ToString());
        AdministrationResult<SourceApplicationOutcome> outcome = await applications.ApplyAsync(CallerId, request.ExternalContributionId!.Value, requestKey, cancellationToken);
        if (!outcome.Succeeded)
        {
            return Failure(outcome.Error);
        }

        if (outcome.Value.Replayed)
        {
            Response.Headers[ReplayedHeader] = "true";
        }

        return Created($"/{Collection}/{outcome.Value.Application.Id}", outcome.Value.Application);
    }

    /// <summary>
    /// Confirms, on the revision's latest CONFLICT attempt, that the accepted values apply to the source record as it now is; the next attempt
    /// expects that version (WF-13 US-EXT-REV-017). Not an edit of the values.
    /// </summary>
    [HttpPost("{sourceApplicationId:guid}/revalidate")]
    [RequirePermission(PermissionCatalogue.ExternalContributionApply)]
    [SensitiveWrite]
    [ProducesResponseType<SourceApplicationDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ExternalParticipation_Revalidate")]
    public async Task<IActionResult> Revalidate(Guid sourceApplicationId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await applications.RevalidateAsync(CallerId, sourceApplicationId, version, cancellationToken))
            : problem!;
}
