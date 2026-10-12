using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.AuditActivity;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.AuditActivity.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// FG-06 ADM-050 Audit Trail and ADM-051 Audit Event Detail (TASK-073): formal audit, read by category, scope and sensitivity.
/// Every read is authorised by the module on the event's category permission, never by the caller's role (BR-AUD-023). There is no
/// write: a committed event is never edited or deleted (AUD-CC-06), so <c>PUT</c>, <c>PATCH</c> and <c>DELETE</c> answer 405.
/// </summary>
[Route(Collection)]
[Tags("AuditActivity")]
public sealed class AuditEventsController(IAuditEventService events) : AdministrationControllerBase
{
    private const string Collection = "api/v1/audit-events";

    /// <summary>
    /// The events the caller may read, newest first (AUD-API-003). Filters: <c>category</c>, <c>eventClass</c> and <c>outcome</c>
    /// (sets), <c>occurredFrom</c>/<c>occurredTo</c>, <c>actorUserId</c>, <c>eventType</c>, <c>projectId</c>, <c>subjectType</c>,
    /// <c>subjectId</c>, <c>correlationId</c>. Without <c>category</c>, every category the caller reads; naming one they may not is 403.
    /// </summary>
    [HttpGet]
    [AllowAnyAuthenticatedUser]
    [ProducesResponseType<AuditEventPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("AuditActivity_ListAuditEvents")]
    public async Task<IActionResult> List(
        [FromQuery] string? category, [FromQuery] DateTimeOffset? occurredFrom, [FromQuery] DateTimeOffset? occurredTo, [FromQuery] Guid? actorUserId,
        [FromQuery] string? eventClass, [FromQuery] string? eventType, [FromQuery] string? outcome, [FromQuery] Guid? projectId, [FromQuery] string? subjectType,
        [FromQuery] Guid? subjectId, [FromQuery] Guid? correlationId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        AuditEventQuery query = AuditActivityQueries.Events(
            category, occurredFrom, occurredTo, actorUserId, eventClass, eventType, outcome, projectId, subjectType, subjectId, correlationId, errors);
        PageRequest paging = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0 ? ValidationFailed(errors) : Respond(await events.SearchAsync(CallerId, query, paging, cancellationToken));
    }

    /// <summary>One event with its attributes, integrity metadata and SIEM forwarding (AUD-API-004). 404 for an event the caller may not read.</summary>
    [HttpGet("{auditEventId:guid}")]
    [AllowAnyAuthenticatedUser]
    [ProducesResponseType<AuditEventDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("AuditActivity_GetAuditEvent")]
    public async Task<IActionResult> Get(Guid auditEventId, CancellationToken cancellationToken) =>
        Respond(await events.GetAsync(CallerId, auditEventId, cancellationToken));
}

/// <summary>What the caller may do with audit and Activity (AUD-API-014), for the SPA to show; the API enforces it again on every read.</summary>
[Route("api/v1/audit-access")]
[Tags("AuditActivity")]
public sealed class AuditAccessController(IAuditEventService events) : AdministrationControllerBase
{
    [HttpGet]
    [AllowAnyAuthenticatedUser]
    [ProducesResponseType<AuditAccessView>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("AuditActivity_GetAuditAccess")]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) => Ok(await events.GetAccessAsync(CallerId, cancellationToken));
}

/// <summary>
/// FG-06 SCR-057 Project Activity and the source-record timeline (TASK-073; AUD-API-001, -002): business-readable lines derived from
/// the audit store, on the projects the caller may see now. Filters: <c>projectId</c>, <c>subjectType</c>, <c>subjectId</c>,
/// <c>activityKind</c> (a set of domains), <c>actorUserId</c>, <c>occurredFrom</c>/<c>occurredTo</c>. Newest first.
/// </summary>
[Route("api/v1/activity-entries")]
[Tags("AuditActivity")]
public sealed class ActivityEntriesController(IActivityService activity) : AdministrationControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCatalogue.ActivityView)]
    [ProducesResponseType<ActivityEntryPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("AuditActivity_ListActivityEntries")]
    public async Task<IActionResult> List(
        [FromQuery] Guid? projectId, [FromQuery] string? subjectType, [FromQuery] Guid? subjectId, [FromQuery] string? activityKind, [FromQuery] Guid? actorUserId,
        [FromQuery] DateTimeOffset? occurredFrom, [FromQuery] DateTimeOffset? occurredTo, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        ActivityQuery query = AuditActivityQueries.Activity(projectId, subjectType, subjectId, activityKind, actorUserId, occurredFrom, occurredTo, errors);
        PageRequest paging = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0 ? ValidationFailed(errors) : Ok(await activity.ListAsync(CallerId, query, paging, cancellationToken));
    }
}
