namespace PMPlatform.Application.Features.IdentityAccess.Contracts;

/// <summary>How the session's user proved their identity.</summary>
public enum AuthenticationMethod
{
    /// <summary>A bind to AHDA's directory with the user's own credentials.</summary>
    Directory = 1,

    /// <summary>OpenID Connect single sign-on at AHDA's identity provider.</summary>
    SingleSignOn = 2,

    /// <summary>A local account's password, held by the platform (the 5 Nov 2026 review §6.1).</summary>
    LocalPassword = 3,

    /// <summary>A passkey on the person's device, unlocked with Face ID, Touch ID or the device's own unlock (13 Nov 2026 review).</summary>
    Passkey = 4,

    /// <summary>A one-time code sent by SMS to the person's verified mobile number (13 Nov 2026 review).</summary>
    SmsCode = 5,
}
