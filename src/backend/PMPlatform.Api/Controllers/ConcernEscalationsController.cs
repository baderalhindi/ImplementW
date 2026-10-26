using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Api.Models.ManagementConcern;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.ManagementConcern.Contracts;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// WF-07 (TASK-057): escalations of issues and challenges — MOD-039, SCR-087, SCR-088. Raising one publishes exactly one
/// NotificationIntent to WF-15; a retry with the same <c>Idempotency-Key</c> answers with the escalation it raised and
/// <c>Idempotent-Replayed: true</c>, and publishes nothing (R-37). An escalation never changes its concern's status.
/// </summary>
[Route(Collection)]
[Tags("ManagementConcern")]
public sealed class ConcernEscalationsController(IConcernEscalationService escalations) : AdministrationControllerBase
{
    public const string ReplayedHeader = "Idempotent-Replayed";

    private const string Collection = "api/v1/concern-escalations";

    /// <summary>The concern's escalations, newest first. <c>managementConcernId</c> is required (R-3).</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.ConcernView)]
    [ProducesResponseType<ConcernEscalationPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ManagementConcern_ListConcernEscalations")]
    public async Task<IActionResult> List([FromQuery] Guid? managementConcernId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(managementConcernId, "managementConcernId", errors);
        PageRequest paging = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0 ? ValidationFailed(errors) : Ok(await escalations.ListAsync(CallerId, managementConcernId!.Value, paging, cancellationToken));
    }

    [HttpGet("{escalationId:guid}")]
    [RequirePermission(PermissionCatalogue.ConcernView)]
    [ProducesResponseType<ConcernEscalationDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ManagementConcern_GetConcernEscalation")]
    public async Task<IActionResult> Get(Guid escalationId, CancellationToken cancellationToken) =>
        Respond(await escalations.GetAsync(CallerId, escalationId, cancellationToken));

    /// <summary>
    /// Escalates an open concern (MOD-039) to the role WORKFLOW_POLICY routes escalations to. Internal users only (ADR-013). A retry
    /// with the same key and body answers 201 with the same escalation and <c>Idempotent-Replayed: true</c>.
    /// </summary>
    [HttpPost]
    [RequirePermission(PermissionCatalogue.ConcernEscalate)]
    [SensitiveWrite]
    [ProducesResponseType<ConcernEscalationDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("ManagementConcern_EscalateManagementConcern")]
    public async Task<IActionResult> Escalate(ConcernEscalationCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // [SensitiveWrite] has already refused a request without a uuid key (R-36).
        Guid requestKey = Guid.Parse(Request.Headers[SensitiveWriteAttribute.HeaderName].ToString());
        if (request.Validate(requestKey, out ConcernEscalationDraft? draft) is { Count: > 0 } errors)
        {
            return ValidationFailed(errors);
        }

        AdministrationResult<EscalationOutcome> outcome = await escalations.EscalateAsync(CallerId, draft!, cancellationToken);
        if (!outcome.Succeeded)
        {
            return Failure(outcome.Error);
        }

        if (outcome.Value.Replayed)
        {
            Response.Headers[ReplayedHeader] = "true";
        }

        return Created($"/{Collection}/{outcome.Value.Escalation.Id}", outcome.Value.Escalation);
    }

    /// <summary>OPEN → RESOLVED, with the direction given. Held through the role the escalation is addressed to; internal users only.</summary>
    [HttpPost("{escalationId:guid}/resolve")]
    [RequirePermission(PermissionCatalogue.ConcernEscalationResolve)]
    [SensitiveWrite]
    [ProducesResponseType<ConcernEscalationDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ManagementConcern_ResolveConcernEscalation")]
    public async Task<IActionResult> Resolve(Guid escalationId, ConcernResolutionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return command.Validate(out NarrativeText? resolution) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : Respond(await escalations.ResolveAsync(CallerId, escalationId, resolution!, cancellationToken));
    }

    /// <summary>OPEN → WITHDRAWN, by its escalator.</summary>
    [HttpPost("{escalationId:guid}/withdraw")]
    [RequirePermission(PermissionCatalogue.ConcernEscalate)]
    [SensitiveWrite]
    [ProducesResponseType<ConcernEscalationDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ManagementConcern_WithdrawConcernEscalation")]
    public async Task<IActionResult> Withdraw(Guid escalationId, CancellationToken cancellationToken) =>
        Respond(await escalations.WithdrawAsync(CallerId, escalationId, cancellationToken));
}
