using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.Notifications;

/// <summary>The e-mail channel: AHDA's Exchange SMTP relay (<c>EXCHANGE_SMTP_*</c>). Plain text only.</summary>
public interface IEmailSender
{
    /// <summary>False when no relay is configured; the channel is then not used.</summary>
    public bool IsConfigured { get; }

    /// <summary>Hands the message to the relay; returns the relay's message id, if it gave one. Throws when the relay did not accept it.</summary>
    public Task<string?> SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

/// <summary>One plain-text message to one recipient.</summary>
public sealed record EmailMessage(string To, string Subject, string Body, Language Language);
