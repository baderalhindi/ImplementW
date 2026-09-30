using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.Notifications.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Notifications;

namespace PMPlatform.Api.Controllers;

/// <summary>WF-15 operations (TASK-039): deliveries by outcome — the dead letters above all — and their redrive.</summary>
[Route(Collection)]
[Tags("Notifications")]
public sealed class NotificationDeliveriesController(INotificationOperations operations) : AdministrationControllerBase
{
    private const string Collection = "api/v1/notification-deliveries";

    /// <summary>Most recently changed first. Filters: <c>status</c>, <c>channel</c> (sets), e.g. <c>status=DEAD_LETTER</c>.</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.NotificationDeliveryView)]
    [ProducesResponseType<NotificationDeliveryPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Notifications_ListNotificationDeliveries")]
    public async Task<IActionResult> List(
        [FromQuery] string? status, [FromQuery] string? channel, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        NotificationDeliveryQuery query = new(
            QueryParameters.EnumSet<NotificationDeliveryStatus>(status, "status", errors),
            QueryParameters.EnumSet<NotificationChannel>(channel, "channel", errors),
            QueryParameters.Page(page, pageSize, errors));
        return errors.Count > 0 ? ValidationFailed(errors) : Ok(await operations.ListDeliveriesAsync(query, cancellationToken));
    }

    /// <summary>DEAD_LETTER → PENDING with its attempts reset, sent on the next pass; any other state is 409 <c>INVALID_TRANSITION</c>.</summary>
    [HttpPost("{deliveryId:guid}/redrive")]
    [RequirePermission(PermissionCatalogue.NotificationDeliveryManage)]
    [SensitiveWrite]
    [ProducesResponseType<NotificationDeliverySummary>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Notifications_RedriveNotificationDelivery")]
    public async Task<IActionResult> Redrive(Guid deliveryId, CancellationToken cancellationToken) =>
        Respond(await operations.RedriveDeliveryAsync(CallerId, deliveryId, cancellationToken));
}
