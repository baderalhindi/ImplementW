using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Api.Models.Notifications;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.Notifications.Contracts;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// WF-15 notification templates (TASK-039): bilingual and mandatory (ADR-012), per event type and channel, under the
/// governed lifecycle — author, reviewer and publisher are three people. Never deleted (RETAIN).
/// </summary>
[Route(Collection)]
[Tags("Notifications")]
public sealed class NotificationTemplatesController(INotificationTemplateAdministration templates) : AdministrationControllerBase
{
    private const string Collection = "api/v1/notification-templates";

    /// <summary>Filters: <c>eventType</c>, <c>eventFamilyCode</c>, <c>channel</c> and <c>lifecycleState</c> (sets). By event type, channel, newest version first.</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.NotificationTemplateView)]
    [ProducesResponseType<NotificationTemplatePage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Notifications_ListNotificationTemplates")]
    public async Task<IActionResult> List(
        [FromQuery] string? eventType, [FromQuery] string? eventFamilyCode, [FromQuery] string? channel, [FromQuery] string? lifecycleState,
        [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        NotificationTemplateQuery query = new(
            string.IsNullOrEmpty(eventType) ? null : eventType,
            string.IsNullOrEmpty(eventFamilyCode) ? null : eventFamilyCode,
            QueryParameters.EnumSet<NotificationChannel>(channel, "channel", errors),
            QueryParameters.EnumSet<GovernedLifecycleState>(lifecycleState, "lifecycleState", errors),
            QueryParameters.Page(page, pageSize, errors));
        return errors.Count > 0 ? ValidationFailed(errors) : Ok(await templates.ListAsync(query, cancellationToken));
    }

    [HttpGet("{templateId:guid}")]
    [RequirePermission(PermissionCatalogue.NotificationTemplateView)]
    [ProducesResponseType<NotificationTemplateDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Notifications_GetNotificationTemplate")]
    public async Task<IActionResult> Get(Guid templateId, CancellationToken cancellationToken) =>
        Respond(await templates.GetAsync(templateId, cancellationToken));

    /// <summary>A new version of the event type and channel, DRAFT, numbered after the last.</summary>
    [HttpPost]
    [RequirePermission(PermissionCatalogue.NotificationTemplateManage)]
    [SensitiveWrite]
    [ProducesResponseType<NotificationTemplateDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("Notifications_CreateNotificationTemplate")]
    public async Task<IActionResult> Create(NotificationTemplateCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate(out NotificationTemplateDraft? draft) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : RespondCreated(await templates.CreateAsync(CallerId, draft!, cancellationToken), $"/{Collection}", t => t.Id);
    }

    /// <summary>A DRAFT's text, by its author. Requires <c>If-Match</c> (R-21).</summary>
    [HttpPut("{templateId:guid}")]
    [RequirePermission(PermissionCatalogue.NotificationTemplateManage)]
    [SensitiveWrite]
    [ProducesResponseType<NotificationTemplateDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Notifications_UpdateNotificationTemplate")]
    public async Task<IActionResult> Update(Guid templateId, NotificationTemplateUpdateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return !TryReadIfMatch(required: true, out uint? version, out IActionResult? problem) ? problem!
            : request.Validate(out NotificationTemplateChanges? changes) is { Count: > 0 } errors ? ValidationFailed(errors)
            : Respond(await templates.UpdateAsync(CallerId, templateId, changes!, version!.Value, cancellationToken));
    }

    [HttpPost("{templateId:guid}/validate")]
    [RequirePermission(PermissionCatalogue.NotificationTemplateManage)]
    [SensitiveWrite]
    [ProducesResponseType<NotificationTemplateDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Notifications_ValidateNotificationTemplate")]
    public async Task<IActionResult> Validate(Guid templateId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await templates.ValidateAsync(CallerId, templateId, version, cancellationToken))
            : problem!;

    /// <summary>Retires the version it replaces in the same change.</summary>
    [HttpPost("{templateId:guid}/publish")]
    [RequirePermission(PermissionCatalogue.NotificationTemplateManage)]
    [SensitiveWrite]
    [ProducesResponseType<NotificationTemplateDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Notifications_PublishNotificationTemplate")]
    public async Task<IActionResult> Publish(Guid templateId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await templates.PublishAsync(CallerId, templateId, version, cancellationToken))
            : problem!;

    [HttpPost("{templateId:guid}/retire")]
    [RequirePermission(PermissionCatalogue.NotificationTemplateManage)]
    [SensitiveWrite]
    [ProducesResponseType<NotificationTemplateDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Notifications_RetireNotificationTemplate")]
    public async Task<IActionResult> Retire(Guid templateId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await templates.RetireAsync(CallerId, templateId, version, cancellationToken))
            : problem!;
}
