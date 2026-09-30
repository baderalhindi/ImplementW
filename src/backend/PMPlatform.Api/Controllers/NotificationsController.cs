using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Features.Notifications.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Notifications;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// The caller's own notifications (TASK-039, for TASK-040's SCR-150–153 and MOD-070–071). Every operation acts on the
/// caller's deliveries only, so a valid session is its whole protection; another person's notification is 404 (R-47).
/// </summary>
[Route(Collection)]
[Tags("Notifications")]
public sealed class NotificationsController(INotificationInbox inbox) : AdministrationControllerBase
{
    private const string Collection = "api/v1/notifications";

    /// <summary>In-app notifications, newest first. Filters: <c>unread</c> (true or false), <c>eventFamilyCode</c>.</summary>
    [HttpGet]
    [AllowAnyAuthenticatedUser]
    [ProducesResponseType<NotificationPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Notifications_ListNotifications")]
    public async Task<IActionResult> List(
        [FromQuery] bool? unread, [FromQuery] string? eventFamilyCode, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        NotificationInboxQuery query = new(unread, string.IsNullOrEmpty(eventFamilyCode) ? null : eventFamilyCode, QueryParameters.Page(page, pageSize, errors));
        return errors.Count > 0 ? ValidationFailed(errors) : Ok(await inbox.ListAsync(CallerId, query, cancellationToken));
    }

    /// <summary>The unread badge (TASK-040).</summary>
    [HttpGet("unread-count")]
    [AllowAnyAuthenticatedUser]
    [ProducesResponseType<UnreadNotificationCount>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Notifications_CountUnreadNotifications")]
    public async Task<IActionResult> CountUnread(CancellationToken cancellationToken) =>
        Ok(await inbox.CountUnreadAsync(CallerId, cancellationToken));

    /// <summary>SCR-153: every delivery to the caller on every channel, with its outcome. Filters: <c>channel</c>, <c>status</c> (sets).</summary>
    [HttpGet("history")]
    [AllowAnyAuthenticatedUser]
    [ProducesResponseType<NotificationHistoryPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Notifications_ListNotificationHistory")]
    public async Task<IActionResult> History(
        [FromQuery] string? channel, [FromQuery] string? status, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        NotificationHistoryQuery query = new(
            QueryParameters.EnumSet<NotificationChannel>(channel, "channel", errors),
            QueryParameters.EnumSet<NotificationDeliveryStatus>(status, "status", errors),
            QueryParameters.Page(page, pageSize, errors));
        return errors.Count > 0 ? ValidationFailed(errors) : Ok(await inbox.ListHistoryAsync(CallerId, query, cancellationToken));
    }

    /// <summary>MOD-070 Notification Detail.</summary>
    [HttpGet("{notificationId:guid}")]
    [AllowAnyAuthenticatedUser]
    [ProducesResponseType<NotificationSummary>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Notifications_GetNotification")]
    public async Task<IActionResult> Get(Guid notificationId, CancellationToken cancellationToken) =>
        Respond(await inbox.GetAsync(CallerId, notificationId, cancellationToken));

    /// <summary>Marks it read; reading it again changes nothing.</summary>
    [HttpPost("{notificationId:guid}/read")]
    [AllowAnyAuthenticatedUser]
    [NonSensitiveWrite("R-35: marking a notification read")]
    [ProducesResponseType<NotificationSummary>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Notifications_MarkNotificationRead")]
    public async Task<IActionResult> MarkRead(Guid notificationId, CancellationToken cancellationToken) =>
        Respond(await inbox.MarkReadAsync(CallerId, notificationId, cancellationToken));

    /// <summary>MOD-071 Mark All Read: every notification sent until now; later ones arrive unread.</summary>
    [HttpPost("read-all")]
    [AllowAnyAuthenticatedUser]
    [NonSensitiveWrite("R-35: marking a notification read")]
    [ProducesResponseType<NotificationsMarkedRead>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Notifications_MarkAllNotificationsRead")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken) =>
        Ok(await inbox.MarkAllReadAsync(CallerId, cancellationToken));
}
