using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.Notifications.Contracts;
using PMPlatform.Domain.Notifications;

namespace PMPlatform.Api.Controllers;

/// <summary>WF-15 operations (TASK-039): what was received, where it went, and the redrive of an intent that failed to route.</summary>
[Route(Collection)]
[Tags("Notifications")]
public sealed class NotificationIntentsController(INotificationOperations operations) : AdministrationControllerBase
{
    private const string Collection = "api/v1/notification-intents";

    /// <summary>Newest first. Filters: <c>status</c> (a set), <c>eventFamilyCode</c>, <c>sourceModule</c>.</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.NotificationDeliveryView)]
    [ProducesResponseType<NotificationIntentPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Notifications_ListNotificationIntents")]
    public async Task<IActionResult> List(
        [FromQuery] string? status, [FromQuery] string? eventFamilyCode, [FromQuery] string? sourceModule,
        [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        NotificationIntentQuery query = new(
            QueryParameters.EnumSet<NotificationIntentStatus>(status, "status", errors),
            string.IsNullOrEmpty(eventFamilyCode) ? null : eventFamilyCode,
            string.IsNullOrEmpty(sourceModule) ? null : sourceModule,
            QueryParameters.Page(page, pageSize, errors));
        return errors.Count > 0 ? ValidationFailed(errors) : Ok(await operations.ListIntentsAsync(query, cancellationToken));
    }

    /// <summary>The intent and every delivery it produced, without their rendered content.</summary>
    [HttpGet("{intentId:guid}")]
    [RequirePermission(PermissionCatalogue.NotificationDeliveryView)]
    [ProducesResponseType<NotificationIntentDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Notifications_GetNotificationIntent")]
    public async Task<IActionResult> Get(Guid intentId, CancellationToken cancellationToken) =>
        Respond(await operations.GetIntentAsync(intentId, cancellationToken));

    /// <summary>FAILED → RECEIVED, routed again on the next pass; any other state is 409 <c>INVALID_TRANSITION</c>.</summary>
    [HttpPost("{intentId:guid}/redrive")]
    [RequirePermission(PermissionCatalogue.NotificationDeliveryManage)]
    [SensitiveWrite]
    [ProducesResponseType<NotificationIntentDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Notifications_RedriveNotificationIntent")]
    public async Task<IActionResult> Redrive(Guid intentId, CancellationToken cancellationToken) =>
        Respond(await operations.RedriveIntentAsync(CallerId, intentId, cancellationToken));
}
