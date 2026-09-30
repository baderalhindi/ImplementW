using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Models.Notifications;
using PMPlatform.Application.Features.Notifications.Contracts;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// SCR-154: the caller's own channels per family (ADR-004). In-app and every channel of a mandatory family are shown and
/// cannot be turned off; the routing and sending passes read the rest on every send.
/// </summary>
[Route(Collection)]
[Tags("Notifications")]
public sealed class NotificationPreferencesController(INotificationInbox inbox) : AdministrationControllerBase
{
    private const string Collection = "api/v1/notification-preferences";

    /// <summary>422 <c>CONFIGURATION_MISSING</c> while no NOTIFICATION_ROUTING is in force.</summary>
    [HttpGet]
    [AllowAnyAuthenticatedUser]
    [ProducesResponseType<NotificationPreferenceSet>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Notifications_GetNotificationPreferences")]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        Ok(await inbox.GetPreferencesAsync(CallerId, cancellationToken));

    /// <summary>
    /// Sets the listed choices, all or none. No <c>If-Match</c>: the set spans many rows with no one version, and each item
    /// states its whole value, so a repeated or concurrent request by the same person converges (notification-runtime.md F-10).
    /// </summary>
    [HttpPut]
    [AllowAnyAuthenticatedUser]
    [NonSensitiveWrite("R-35: user preferences")]
    [ProducesResponseType<NotificationPreferenceSet>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Notifications_UpdateNotificationPreferences")]
    public async Task<IActionResult> Update(NotificationPreferencesUpdateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate(out IReadOnlyList<NotificationPreferenceChange>? changes) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : Respond(await inbox.UpdatePreferencesAsync(CallerId, changes!, cancellationToken));
    }
}
