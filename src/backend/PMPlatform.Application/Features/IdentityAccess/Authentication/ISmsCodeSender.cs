namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>Delivers a one-time code's text by SMS. Infrastructure adapts the platform's SMS gateway to it.</summary>
public interface ISmsCodeSender
{
    public bool IsConfigured { get; }

    /// <summary>Throws when the provider did not accept the message.</summary>
    public Task SendAsync(string mobileNumber, string text, CancellationToken cancellationToken);
}
