namespace PMPlatform.Application.Features.Notifications;

/// <summary>
/// The SMS channel's provider (ADR-004): a CST-licensed gateway with a registered sender ID. None is selected yet (OQ-012):
/// TASK-103 adds the adapter, its <c>SMS_PROVIDER_*</c> settings and the delivery-receipt callback. Until then the gateway
/// is not configured and every SMS delivery is suppressed, while in-app and e-mail carry the notification.
/// </summary>
public interface ISmsGateway
{
    public bool IsConfigured { get; }

    /// <summary>Submits the text to the number; returns the provider's message id. Throws when the provider did not accept it.</summary>
    public Task<string?> SendAsync(string mobileNumber, string text, CancellationToken cancellationToken);
}
