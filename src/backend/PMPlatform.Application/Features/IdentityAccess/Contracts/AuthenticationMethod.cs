namespace PMPlatform.Application.Features.IdentityAccess.Contracts;

/// <summary>How the session's user proved their identity.</summary>
public enum AuthenticationMethod
{
    /// <summary>A bind to AHDA's directory with the user's own credentials.</summary>
    Directory = 1,

    /// <summary>OpenID Connect single sign-on at AHDA's identity provider.</summary>
    SingleSignOn = 2,
}
