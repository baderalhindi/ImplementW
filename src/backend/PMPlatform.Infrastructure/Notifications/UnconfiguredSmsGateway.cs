using PMPlatform.Application.Features.Notifications;

namespace PMPlatform.Infrastructure.Notifications;

/// <summary>
/// No SMS provider is selected (OQ-012; notification-runtime.md F-2). Routing sees the gateway is not configured and
/// suppresses every SMS delivery as CHANNEL_NOT_CONFIGURED; in-app and e-mail still carry the notification. TASK-103's
/// provider adapter replaces this one and reads <c>SMS_PROVIDER_*</c>.
/// </summary>
internal sealed class UnconfiguredSmsGateway : ISmsGateway
{
    public bool IsConfigured => false;

    public Task<string?> SendAsync(string mobileNumber, string text, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("No SMS provider is configured.");
}
